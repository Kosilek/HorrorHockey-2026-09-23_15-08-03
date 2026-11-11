using Codice.Client.BaseCommands;
using ContractsServer.Data;
using ContractsServer.Service.Navigation;
using Domain.Game.Core.Entity;
using PlasticPipe.PlasticProtocol.Server.Stubs;
using Server.Core.Enums;
using Shared.Core.Contracts;
using System.Collections.Generic;
using UnityEngine;
using UnityLife.Game.Entity.Animations;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace UnityLife.Game.Entity.Navigations
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Entity/EntityAI")]
    public class AgentAI : MonoBehaviour
    {
        //[SerializeField] private Transform[] _points;
        [Tooltip("Плотность curve на каждом отрезке. 0.05 — как Bezier Step у FlatMap. Меньше — плавнее поворот.")]
        [SerializeField, Min(0.01f)] private float _curveStep = GameConstants.CurveStep;
        [SerializeField] private float _maxSpeed = GameConstants.MaxSpeed;
        [SerializeField, Min(0f)] private float _accelTime = GameConstants.AccelTime;
        [SerializeField, Min(0f)] private float _brakeTime = GameConstants.BrakeTime;
        [SerializeField] private bool _faceMoveDirection = true;
        [SerializeField] private float _turnSpeed = 720f;
        [Tooltip("Left и Right, если весь поворот больше этого угла и корпус ещё крутится. Иначе Move.")]
        [SerializeField, Min(0f)] private float _turnAnimMinAngle = 30f;
        [Header("Tilt")]
        [SerializeField] private float _maxTilt = 20f;
        [SerializeField] private float _tiltSmooth = 8f;
        [SerializeField] private float _tiltStrength = 2f;
        [Header("Server sync")]
        [Tooltip("Как быстро тело плавно доезжает до серверной позиции, если разошлось с ней. Больше — быстрее.")]
        [SerializeField, Min(0.1f)] private float _correctionSharpness = 6f;
        [Header("Face target")]
        [Tooltip("За сколько метров до конца маршрута с остановкой корпус начинает доворачиваться к цели. Стоя смотрит на неё всегда.")]
        [SerializeField, Min(0f)] private float _faceTargetDistance = 1.5f;

        private Animator _animator;
        private Vector2 _visualOffset;
        private IEntityMovemet _target;

        private const float TurnEndAngle = 0.75f;
        private const int MoveIndex = 0;
        private const int StopIndex = 1;
        private const int LeftIndex = 2;
        private const int RightIndex = 3;
        private const int AttackIndex = 4;
        private const int IdleIndex = 5;
        private const int FallIndex = 6;
        private const int DeathIndex = 7;
        private static readonly string[] TriggerNames = { "Move", "Stop", "Left", "Right", "Attack", "Idle", "Fall", "Death" };

        private readonly UnitRouteMotion _motion = new();
        private float _groundHeight;
        private float _currentTilt;
        private bool _isStopping;
        private int _turnSide;
        private PathTurn[] _turns = System.Array.Empty<PathTurn>();
        private int _latchedTurn = -1;
        private bool _alignLatched;
        private int _alignSide;
        private bool _animReady;
        private EntityId _animatorId;
        private AnimTrigger[] _triggers = System.Array.Empty<AnimTrigger>();
        private bool _clipLock;
        private bool _clipEntered;
        private bool _clipLoops;
        private bool _clipRestart;
        private int _clipStateHash;
        private int _stateAtPlay;

        /// <summary>
        /// <para>{EN} True while the march is running.</para>
        /// {RU} true, пока марш идёт.
        /// </summary>
        public bool IsMoving => _motion.IsMoving;

        /// <summary>
        /// <para>{EN} Current speed in meters per second.</para>
        /// {RU} Текущая speed, м/с.
        /// </summary>
        public float CurrentSpeed => _motion.CurrentSpeed;

        /// <summary>
        /// <para>{EN} Body roll in degrees. Positive leans into a right turn.</para>
        /// {RU} Наклон тела в градусах. Плюс — вправо, внутрь правого поворота.
        /// </summary>
        public float CurrentTilt => _currentTilt;

        /// <summary>
        /// <para>{EN} Distance already traveled along the path.</para>
        /// {RU} Пройденная длина пути.
        /// </summary>
        public float PathProgress => _motion.PathProgress;

        /// <summary>
        /// <para>{EN} Full path length.</para>
        /// {RU} Полная длина пути.
        /// </summary>
        public float PathLength => _motion.PathLength;

        public bool IsStun { get; private set; }

        public void Initialize(Animator animator)
        {
            _animator = animator;
            _motion.Initialize(_maxSpeed, _accelTime, _brakeTime, new FMVector2(transform.position.x, transform.position.z));
        }

        /// <summary>
        /// <para>{EN} March from where the body already stands through the points. Call from the inspector button.</para>
        /// {RU} Марш от текущей позиции тела через список точек. Вызывай с кнопки в инспекторе.
        /// </summary>
        public void StartMovement(FMVector2[] points)
        {
            Begin(new FMVector2(transform.position.x, transform.position.z), points, stopAtEnd: true);
        }

        /// <summary>
        /// <para>{EN} Server route. The curve is the server one from start. If the body stands elsewhere, it slides onto the curve smoothly, without a jump.</para>
        /// {RU} Маршрут с сервера. Кривая — серверная от start. Если тело стоит в другом месте, оно плавно съезжает на кривую, без прыжка.
        /// </summary>
        public void StartMovement(FMVector2 start, FMVector2[] controls)
        {
            Begin(start, controls, stopAtEnd: true);
        }

        /// <summary>
        /// <para>{EN} Server route with braking that the unit enters on the run: starts at startSpeed and keeps the tilt, brakes at the end.</para>
        /// {RU} Маршрут с сервера с торможением, в который юнит входит на ходу: стартует со скоростью startSpeed и сохраняет наклон, в конце тормозит.
        /// </summary>
        public void StartMovement(FMVector2 start, FMVector2[] controls, float startSpeed)
        {
            Begin(start, controls, stopAtEnd: true, startSpeed);
        }

        /// <summary>
        /// <para>{EN} Server route without braking. Enters at startSpeed and keeps its speed and tilt. Slides onto the curve smoothly like StartMovement.</para>
        /// {RU} Маршрут с сервера без торможения. Входит со скоростью startSpeed и сохраняет скорость и наклон. На кривую съезжает плавно, как StartMovement.
        /// </summary>
        public void StartMovingRoute(FMVector2 start, FMVector2[] controls, float startSpeed)
        {
            Begin(start, controls, stopAtEnd: false, startSpeed);
        }

        /// <summary>
        /// <para>{EN} Plays this clip from the start. Attack, Idle, Fall and Death hold the body until the clip ends or a new route replaces them.</para>
        /// {RU} Проигрывает этот клип с начала. Attack, Idle, Fall и Death держат тело, пока клип не кончится или его не сменит новый маршрут.
        /// </summary>
        public void StartAnimationClip(byte animationClip)
        {
            EnsureAnim();
            if (_animator == null)
                return;

            int index = MappingAnimationClip((StateAnimation)animationClip);
            if (!_triggers[index].Exists)
                return;

            if (HoldsLocomotion(index))
                ArmClip();
            else
                ReleaseClip();

            Play(index, restart: true);
        }

        private int MappingAnimationClip(StateAnimation state)
        {
            return state switch
            {
                StateAnimation.Attack => AttackIndex,
                StateAnimation.Move => MoveIndex,
                StateAnimation.Idle => IdleIndex,
                StateAnimation.Fall => FallIndex,
                StateAnimation.Death => DeathIndex,
                _ => IdleIndex,
            };
        }

        private static bool HoldsLocomotion(int index)
        {
            return index is AttackIndex or IdleIndex or FallIndex or DeathIndex;
        }

        /// <summary>
        /// <para>{EN} Server slow: speed drops to this value and climbs back over two accel times.</para>
        /// {RU} Замедление с сервера: скорость падает до этого значения и восстанавливается за два времени разгона.
        /// </summary>
        public void SlowTo(float speed)
        {
            _motion.SlowTo(speed);
        }

        /// <summary>
        /// <para>{EN} Server stun: the body stops at once and keeps the route.</para>
        /// {RU} Стан с сервера: тело сразу встаёт и сохраняет маршрут.
        /// </summary>
        public void PauseMovement()
        {
            _motion.Pause();
            _isStopping = false;
            _turnSide = 0;
            ApplyAnim();
            IsStun = true;
        }

        /// <summary>
        /// <para>{EN} Stun is over: the same route continues with the usual accel.</para>
        /// {RU} Стан снят: тот же маршрут продолжается обычным разгоном.
        /// </summary>
        public void ResumeMovement()
        {
            IsStun = false;
            _motion.Resume();
            UpdateTurn();
            ApplyAnim();
        }

        /// <summary>
        /// <para>{EN} Target from the server. On the last meters of a route that stops and while standing the body turns to face it.
        /// Not while stunned: the turn waits until the stun is over. Null drops it.</para>
        /// {RU} Цель с сервера. На последних метрах маршрута с остановкой и стоя тело доворачивается к ней.
        /// В стане нет: доворот ждёт выхода из стана. null сбрасывает.
        /// </summary>
        public void SetTarget(IEntityMovemet target)
        {
            _target = target;
        }

        /// <summary>
        /// <para>{EN} Server dropped the orders: the route is gone, the body stays where it is.</para>
        /// {RU} Сервер сбросил приказ: маршрута нет, тело остаётся на месте.
        /// </summary>
        public void StopMovement()
        {
            ReleaseClip();
            _motion.Stop();
            _isStopping = false;
            _turnSide = 0;
            _latchedTurn = -1;
            _alignLatched = false;
            ApplyAnim();
        }

        private void Begin(FMVector2 start, FMVector2[] points, bool stopAtEnd, float startSpeed = 0f)
        {
            ReleaseClip();
            // Тело остаётся где стоит: расхождение с серверным start рисуется смещением и гаснет в Update.
            Vector3 body = transform.position;
            _visualOffset = new Vector2(body.x - start.X, body.z - start.Y);

            // Наклон гасим только при старте с места: на ходу он сам сойдёт к новой кривой.
            if (stopAtEnd && startSpeed <= 1e-4f)
                _currentTilt = 0f;
            _isStopping = false;
            _turnSide = 0;
            _latchedTurn = -1;
            _alignLatched = false;
            _alignSide = 0;
            _groundHeight = transform.position.y;

            if (!_motion.TryStart(start, points, stopAtEnd, startSpeed))
            {
                ApplyAnim();
                return;
            }

            BuildTurns();
            UpdateTurn();
            ApplyAnim();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            bool correcting = DecayVisualOffset(deltaTime);
            if (_motion.IsMoving && !_motion.IsPaused)
                Tick(deltaTime);
            else if (correcting)
                transform.position = Ground(new Vector2(_motion.Position.X, _motion.Position.Y));

            if (IsStanding())
                FaceTarget(deltaTime);

            UpdateTilt(deltaTime);
        }

        // Маршрут без торможения держит IsMoving и на последней точке, хотя тело там уже стоит.
        private bool IsStanding()
        {
            if (IsStun || _motion.IsPaused)
                return false;

            return !_motion.IsMoving || PathLength - PathProgress <= 1e-4f;
        }

        private void FaceTarget(float deltaTime)
        {
            if (!_faceMoveDirection || !TryGetTargetDirection(out Vector3 direction))
                return;

            RotateToward(direction, deltaTime);
        }

        private bool TryGetTargetDirection(out Vector3 direction)
        {
            direction = default;
            // Уничтоженный юнит Unity сравнивает с null, хотя ссылка через интерфейс ещё жива.
            if (_target == null || (_target is UnityEngine.Object unityObject && unityObject == null))
                return false;

            FMVector2 target = _target.BodyPosition;
            Vector3 body = transform.position;
            direction = new Vector3(target.X - body.x, 0f, target.Y - body.z);
            if (direction.sqrMagnitude < 1e-6f)
                return false;

            direction.Normalize();
            return true;
        }

        // На маршруте с остановкой последние метры курс плавно переходит от касательной к направлению на цель.
        private Vector3 BlendToTarget(Vector3 tangent)
        {
            if (!_motion.StopsAtEnd || _faceTargetDistance <= 1e-4f)
                return tangent;

            float remaining = PathLength - PathProgress;
            if (remaining >= _faceTargetDistance || !TryGetTargetDirection(out Vector3 direction))
                return tangent;

            float weight = 1f - remaining / _faceTargetDistance;
            return Vector3.Slerp(tangent, direction, weight);
        }

        private void RotateToward(Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude < 1e-8f)
                return;

            Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
            if (_turnSpeed <= 0f)
            {
                transform.rotation = look;
                return;
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _turnSpeed * deltaTime);
        }

        private bool DecayVisualOffset(float deltaTime)
        {
            if (_visualOffset == Vector2.zero)
                return false;

            _visualOffset = Vector2.Lerp(_visualOffset, Vector2.zero, 1f - Mathf.Exp(-_correctionSharpness * deltaTime));
            if (_visualOffset.sqrMagnitude < 1e-6f)
                _visualOffset = Vector2.zero;
            return true;
        }

        private void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float remaining = PathLength - PathProgress;
            bool alreadyThere = remaining <= 1e-4f;
            if (!alreadyThere && _motion.StopsAtEnd && !_isStopping && IsBrakeZone(remaining, CurrentSpeed))
                _isStopping = true;

            _motion.Tick(deltaTime);
            if (alreadyThere)
            {
                SnapToMotion();
                if (_motion.StopsAtEnd)
                    FinishMove();
                else
                    ApplyAnim();
                return;
            }

            ApplyPose(deltaTime);
            UpdateTurn();
            if (!_motion.IsMoving)
            {
                if (_motion.StopsAtEnd)
                    FinishMove();
                else
                    ApplyAnim();
                return;
            }

            ApplyAnim();
        }

        private void UpdateTilt(float deltaTime)
        {
            // k = ω / v, поэтому v²·k = v·ω. Плюс — поворот вправо.
            // ω с path, а не с кадра: на прямом куске ломаной yaw корпуса стоит, и кадр давал бы 0.
            float yawRate = PathYawRate();
            float lateralAcceleration = CurrentSpeed * yawRate;
            float targetTilt = Mathf.Atan2(lateralAcceleration, Physics.gravity.magnitude) * Mathf.Rad2Deg * _tiltStrength;
            targetTilt = Mathf.Clamp(targetTilt, -_maxTilt, _maxTilt);
            _currentTilt = Mathf.Lerp(_currentTilt, targetTilt, _tiltSmooth * deltaTime);
        }

        private float PathYawRate()
        {
            if (CurrentSpeed <= 1e-4f || !_motion.HasRoute || PathLength <= 1e-3f)
                return 0f;

            int index = FindTurn(PathProgress);
            if (index >= 0)
            {
                PathTurn turn = _turns[index];
                float length = Mathf.Max(0.05f, turn.End - turn.Start);
                return turn.SignedAngle * Mathf.Deg2Rad / length * CurrentSpeed;
            }

            const float Window = 2f;
            float from = Mathf.Clamp(PathProgress - Window * 0.5f, 0f, PathLength);
            float to = Mathf.Clamp(PathProgress + Window * 0.5f, 0f, PathLength);
            float span = to - from;
            if (span < 0.05f)
                return 0f;

            float deltaYaw = Mathf.DeltaAngle(TangentYaw(from), TangentYaw(to));
            return deltaYaw * Mathf.Deg2Rad / span * CurrentSpeed;
        }

        private bool IsBrakeZone(float remaining, float speed)
        {
            if (_brakeTime <= 1e-4f)
                return false;

            float maxSpeed = Mathf.Max(0f, _maxSpeed);
            float brakeRate = maxSpeed / _brakeTime;
            if (brakeRate <= 1e-4f)
                return false;

            float stopDistance = speed * speed / (2f * brakeRate);
            return remaining <= stopDistance + 1e-3f;
        }

        private void UpdateTurn()
        {
            _turnSide = 0;
            if (!_faceMoveDirection)
            {
                _latchedTurn = -1;
                _alignLatched = false;
                return;
            }

            float bodyYaw = transform.eulerAngles.y;
            int current = FindTurn(PathProgress);
            if (current < 0 && _latchedTurn >= 0 && _latchedTurn < _turns.Length)
            {
                PathTurn latched = _turns[_latchedTurn];
                if (Mathf.Abs(Mathf.DeltaAngle(bodyYaw, latched.ExitYaw)) > TurnEndAngle)
                    current = _latchedTurn;
                else
                    _latchedTurn = -1;
            }

            if (current >= 0)
            {
                PathTurn turn = _turns[current];
                bool big = Mathf.Abs(turn.SignedAngle) >= _turnAnimMinAngle;
                bool stillTurning = Mathf.Abs(Mathf.DeltaAngle(bodyYaw, turn.ExitYaw)) > TurnEndAngle;
                if (big && stillTurning)
                {
                    _turnSide = turn.SignedAngle > 0f ? 1 : -1;
                    _latchedTurn = current;
                    _alignLatched = false;
                    return;
                }

                if (!stillTurning || current != _latchedTurn)
                    _latchedTurn = -1;
            }

            float yaw = SignedYawAlongPath();
            if (_alignLatched)
            {
                if (Mathf.Abs(yaw) <= TurnEndAngle)
                    _alignLatched = false;
                else
                {
                    _turnSide = _alignSide;
                    return;
                }
            }

            if (Mathf.Abs(yaw) < _turnAnimMinAngle)
                return;

            _alignLatched = true;
            _alignSide = yaw > 0f ? 1 : -1;
            _turnSide = _alignSide;
        }

        private void BuildTurns()
        {
            _turns = System.Array.Empty<PathTurn>();
            if (!_motion.HasRoute)
                return;

            const float Step = 0.25f;
            const float Noise = 0.2f;
            const float HoldStraight = 0.75f;

            var turns = new List<PathTurn>(4);
            float prevYaw = TangentYaw(0f);
            bool open = false;
            float start = 0f;
            float accum = 0f;
            float calm = 0f;
            float traveled = 0f;

            while (traveled < PathLength - 1e-4f)
            {
                float next = Mathf.Min(traveled + Step, PathLength);
                float delta = Mathf.DeltaAngle(prevYaw, TangentYaw(next));
                float segment = next - traveled;
                prevYaw = TangentYaw(next);
                traveled = next;

                if (Mathf.Abs(delta) <= Noise)
                {
                    calm += segment;
                    if (open && calm >= HoldStraight)
                    {
                        AddTurn(turns, start, traveled - calm, accum);
                        open = false;
                    }

                    continue;
                }

                calm = 0f;
                int deltaSign = delta > 0f ? 1 : -1;
                if (!open)
                {
                    open = true;
                    start = traveled - segment;
                    accum = delta;
                    continue;
                }

                if (deltaSign != (accum > 0f ? 1 : -1))
                {
                    AddTurn(turns, start, traveled - segment, accum);
                    start = traveled - segment;
                    accum = delta;
                    continue;
                }

                accum += delta;
            }

            if (open)
                AddTurn(turns, start, PathLength, accum);

            _turns = turns.ToArray();
        }

        private void AddTurn(List<PathTurn> turns, float start, float end, float accum)
        {
            if (Mathf.Abs(accum) < 1f)
                return;

            if (end < start)
                end = start;

            turns.Add(new PathTurn
            {
                Start = start,
                End = end,
                SignedAngle = accum,
                ExitYaw = TangentYaw(end)
            });
        }

        private int FindTurn(float distance)
        {
            for (int i = 0; i < _turns.Length; i++)
            {
                if (distance + 1e-3f >= _turns[i].Start && distance <= _turns[i].End + 1e-3f)
                    return i;
            }

            return -1;
        }

        private float TangentYaw(float distance)
        {
            if (!TrySample(distance, out _, out Vector2 tangent))
                return transform.eulerAngles.y;

            if (tangent.sqrMagnitude < 1e-8f)
                return transform.eulerAngles.y;

            return Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg;
        }

        private bool TrySample(float distance, out Vector2 position, out Vector2 tangent)
        {
            position = default;
            tangent = default;
            if (!_motion.TrySample(distance, out FMVector2 sampledPosition, out FMVector2 sampledTangent))
                return false;

            position = new Vector2(sampledPosition.X, sampledPosition.Y);
            tangent = new Vector2(sampledTangent.X, sampledTangent.Y);
            return true;
        }

        private float SignedYawAlongPath()
        {
            if (!TrySample(PathProgress, out _, out Vector2 tangent))
                return 0f;

            if (tangent.sqrMagnitude < 1e-8f)
                return 0f;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-8f)
                return 0f;

            return Vector3.SignedAngle(forward, new Vector3(tangent.x, 0f, tangent.y), Vector3.up);
        }

        private void ApplyAnim()
        {
            if (IsStun || _clipLock)
                return;

            EnsureAnim();
            int desired = DesiredLocomotion();
            if (desired < 0)
                return;

            Play(desired, restart: false);
        }

        // Пока клип держит тело, движение его не перебивает. Новый маршрут и StopMovement снимают удержание.
        // Незацикленный клип отпускает тело сам, когда доиграл. Зацикленный ждёт нового маршрута.
        private void LateUpdate()
        {
            if (!_clipLock || _animator == null)
                return;

            if (_animator.IsInTransition(0))
            {
                _clipEntered = true;
                _clipStateHash = 0;
                return;
            }

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (!_clipEntered || _clipStateHash == 0)
            {
                // Повтор того же клипа остаётся в том же состоянии. Первый запуск без перехода — триггера в контроллере нет.
                if (!_clipEntered && !_clipRestart && state.fullPathHash == _stateAtPlay)
                {
                    ReleaseClip();
                    return;
                }

                LatchClip(state);
                return;
            }

            if (state.fullPathHash != _clipStateHash)
            {
                EndClip();
                return;
            }

            if (state.normalizedTime < 1f || _clipLoops)
                return;

            EndClip();
        }

        private int DesiredLocomotion()
        {
            if (_motion.IsPaused)
                return -1;
            if (_isStopping)
                return StopIndex;
            if (!IsMoving)
                return -1;
            if (_turnSide < 0)
                return LeftIndex;
            if (_turnSide > 0)
                return RightIndex;
            return MoveIndex;
        }

        private void ArmClip()
        {
            _clipRestart = _clipLock && _clipEntered;
            _stateAtPlay = _animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            _clipLock = true;
            _clipEntered = false;
            _clipStateHash = 0;
            _clipLoops = false;
        }

        private void LatchClip(AnimatorStateInfo state)
        {
            _clipEntered = true;
            _clipStateHash = state.fullPathHash;
            AnimatorClipInfo[] playing = _animator.GetCurrentAnimatorClipInfo(0);
            _clipLoops = playing.Length > 0 && playing[0].clip != null && playing[0].clip.isLooping;
        }

        private void ReleaseClip()
        {
            _clipLock = false;
            _clipEntered = false;
            _clipLoops = false;
            _clipRestart = false;
            _clipStateHash = 0;
        }

        // Клип доиграл. Идущее тело снова берёт Move, Stop, Left или Right. Стоящее остаётся в последнем кадре.
        private void EndClip()
        {
            ReleaseClip();
            if (IsMoving && !_motion.IsPaused && !_isStopping && !IsStun)
                ApplyAnim();
        }

        private void Play(int index, bool restart)
        {
            if (_animator == null || (uint)index >= (uint)_triggers.Length || !_triggers[index].Exists)
                return;
            if (!restart && _triggers[index].Armed)
                return;

            for (int i = 0; i < _triggers.Length; i++)
            {
                if (!_triggers[i].Exists)
                    continue;

                bool active = i == index;
                _triggers[i].Armed = active;
                if (!active)
                {
                    _animator.ResetTrigger(_triggers[i].Hash);
                    continue;
                }

                if (restart)
                    _animator.ResetTrigger(_triggers[i].Hash);
                _animator.SetTrigger(_triggers[i].Hash);
            }
        }

        private void EnsureAnim()
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();
            if (_animator == null)
                return;

            EntityId id = _animator.GetEntityId();
            if (_animReady && _animatorId == id)
                return;

            _animatorId = id;
            _animReady = true;
            _triggers = new AnimTrigger[TriggerNames.Length];
            for (int i = 0; i < TriggerNames.Length; i++)
                _triggers[i] = Bind(TriggerNames[i]);
            ReleaseClip();
        }

        private AnimTrigger Bind(string name)
        {
            var slot = new AnimTrigger { Hash = Animator.StringToHash(name) };
            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash != slot.Hash)
                    continue;

                if (parameters[i].type != AnimatorControllerParameterType.Trigger)
                {
                    return slot;
                }

                slot.Exists = true;
                return slot;
            }

            return slot;
        }

        private void OnDisable()
        {
            ReleaseClip();
            _motion.Stop();
            _isStopping = false;
            _turnSide = 0;
            _latchedTurn = -1;
            _alignLatched = false;
            ApplyAnim();
        }

        private void ApplyPose(float deltaTime)
        {
            if (!TrySample(PathProgress, out Vector2 position, out Vector2 tangent))
                return;

            transform.position = Ground(position);
            if (!_faceMoveDirection)
                return;

            if (tangent.sqrMagnitude < 1e-8f)
                return;

            Vector3 heading = BlendToTarget(new Vector3(tangent.x, 0f, tangent.y).normalized);
            RotateToward(heading, deltaTime);
        }

        private void SnapToMotion()
        {
            if (!_motion.HasRoute)
                return;

            transform.position = Ground(new Vector2(_motion.Position.X, _motion.Position.Y));
        }

        private void FinishMove()
        {
            // Stop не сбрасываем на финише: Animator мог ещё не успеть перейти по триггеру.
            _isStopping = true;
            _turnSide = 0;
            _latchedTurn = -1;
            _alignLatched = false;
            ApplyAnim();
        }

        //private void OnDrawGizmosSelected()
        //{
        //    if (_points == null || _points.Length == 0)
        //        return;

        //    int drawn = 0;
        //    int count = 0;
        //    for (int i = 0; i < _points.Length; i++)
        //    {
        //        if (_points[i] != null)
        //            count++;
        //    }

        //    for (int i = 0; i < _points.Length; i++)
        //    {
        //        Transform point = _points[i];
        //        if (point == null)
        //            continue;

        //        drawn++;
        //        Vector3 position = point.position;
        //        if (drawn == 1)
        //            Gizmos.color = new Color(0.2f, 0.9f, 0.35f, 1f);
        //        else if (drawn == count)
        //            Gizmos.color = new Color(0.95f, 0.25f, 0.2f, 1f);
        //        else
        //            Gizmos.color = new Color(0.3f, 0.6f, 1f, 1f);

        //        Gizmos.DrawSphere(position, 0.2f);
        //    }

        //    Vector3[] curve = Application.isPlaying && _route != null && _route.Length > 1
        //        ? _route
        //        : null;
        //    if (curve == null && TryBuildCurve(out Vector3[] live))
        //        curve = live;

        //    if (curve == null || curve.Length < 2)
        //        return;

        //    Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);
        //    for (int i = 1; i < curve.Length; i++)
        //        Gizmos.DrawLine(curve[i - 1], curve[i]);
        //}

        private Vector3 Ground(Vector2 point)
        {
            return new Vector3(point.x + _visualOffset.x, _groundHeight, point.y + _visualOffset.y);
        }

        private struct PathTurn
        {
            public float Start;
            public float End;
            public float SignedAngle;
            public float ExitYaw;
        }

        private struct AnimTrigger
        {
            public int Hash;
            public bool Exists;
            public bool Armed;
        }
    }
}