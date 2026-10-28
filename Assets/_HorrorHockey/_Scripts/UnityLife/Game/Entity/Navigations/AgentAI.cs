using Domain.Game.Core.Entity;
using System.Collections.Generic;
using UnityEngine;
using UnityLife.Game.Entity.Animations;
using FMVector3 = FM.Mathematics.DataTypes.Vector3;

namespace UnityLife.Game.Entity.Navigations
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityBodyTilt))]
    [AddComponentMenu("Entity/EntityAI")]
    public class AgentAI : MonoBehaviour
    {
        //[SerializeField] private Transform[] _points;
        [Tooltip("Плотность curve на каждом отрезке. 0.05 — как Bezier Step у FlatMap. Меньше — плавнее поворот.")]
        [SerializeField, Min(0.01f)] private float _curveStep = 0.05f;
        [SerializeField] private float _maxSpeed = 5f;
        [SerializeField, Min(0f)] private float _accelTime = 1f;
        [SerializeField, Min(0f)] private float _brakeTime = 1f;
        [SerializeField] private bool _faceMoveDirection = true;
        [SerializeField] private float _turnSpeed = 720f;
        [Tooltip("Left и Right, если весь поворот больше этого угла и корпус ещё крутится. Иначе Move.")]
        [SerializeField, Min(0f)] private float _turnAnimMinAngle = 30f;
        [Header("Tilt")]
        [SerializeField] private float _maxTilt = 20f;
        [SerializeField] private float _tiltSmooth = 8f;
        [SerializeField] private float _tiltStrength = 2f;

        private Animator _animator;

        private const float TurnEndAngle = 0.75f;
        private const string MoveParam = "Move";
        private const string StopParam = "Stop";
        private const string LeftParam = "Left";
        private const string RightParam = "Right";

        private Vector3[] _route;
        private float[] _routeDistance;
        private float _pathLength;
        private float _pathProgress;
        private float _currentSpeed;
        private float _currentTilt;
        private bool _isMoving;
        private bool _isStopping;
        private int _turnSide;
        private PathTurn[] _turns = System.Array.Empty<PathTurn>();
        private int _latchedTurn = -1;
        private bool _alignLatched;
        private int _alignSide;
        private bool _animReady;
        private EntityId _animatorId;
        private AnimTrigger _moveAnim;
        private AnimTrigger _stopAnim;
        private AnimTrigger _leftAnim;
        private AnimTrigger _rightAnim;

        /// <summary>
        /// <para>{EN} True while the march is running.</para>
        /// {RU} true, пока марш идёт.
        /// </summary>
        public bool IsMoving => _isMoving;

        /// <summary>
        /// <para>{EN} Current speed in meters per second.</para>
        /// {RU} Текущая speed, м/с.
        /// </summary>
        public float CurrentSpeed => _currentSpeed;

        /// <summary>
        /// <para>{EN} Body roll in degrees. Positive leans into a right turn.</para>
        /// {RU} Наклон тела в градусах. Плюс — вправо, внутрь правого поворота.
        /// </summary>
        public float CurrentTilt => _currentTilt;

        /// <summary>
        /// <para>{EN} Distance already traveled along the path.</para>
        /// {RU} Пройденная длина пути.
        /// </summary>
        public float PathProgress => _pathProgress;

        /// <summary>
        /// <para>{EN} Full path length.</para>
        /// {RU} Полная длина пути.
        /// </summary>
        public float PathLength => _pathLength;

        public void Initialize(Animator animator)
        {
            _animator = animator;
        }

        /// <summary>
        /// <para>{EN} March from where the body already stands through the points. Call from the inspector button.</para>
        /// {RU} Марш от текущей позиции тела через список точек. Вызывай с кнопки в инспекторе.
        /// </summary>
        public void StartMovement(FMVector3[] vector3s)
        {
            _pathProgress = 0f;
            _currentSpeed = 0f;
            _currentTilt = 0f;
            _isStopping = false;
            _turnSide = 0;
            _latchedTurn = -1;
            _alignLatched = false;
            _alignSide = 0;

            if (!TryBuildRoute(vector3s))
            {
                _isMoving = false;
                ApplyAnim();
                return;
            }

            _isMoving = true;
            UpdateTurn();
            ApplyAnim();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            if (_isMoving)
                Tick(deltaTime);

            UpdateTilt(deltaTime);
        }

        private void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            float remaining = _pathLength - _pathProgress;
            if (remaining <= 1e-4f)
            {
                Arrive();
                return;
            }

            if (!_isStopping && IsBrakeZone(remaining, _currentSpeed))
                _isStopping = true;

            _currentSpeed = StepSpeed(remaining, deltaTime);

            float moveDist = _currentSpeed * deltaTime;
            if (moveDist >= remaining)
            {
                _pathProgress = _pathLength;
                _currentSpeed = 0f;
                ApplyPose(deltaTime);
                UpdateTurn();
                Arrive();
                return;
            }

            _pathProgress += moveDist;
            ApplyPose(deltaTime);
            UpdateTurn();
            ApplyAnim();
        }

        private float StepSpeed(float remaining, float deltaTime)
        {
            float maxSpeed = Mathf.Max(0f, _maxSpeed);
            float accelRate = _accelTime > 1e-4f ? maxSpeed / _accelTime : float.PositiveInfinity;
            float brakeRate = _brakeTime > 1e-4f ? maxSpeed / _brakeTime : float.PositiveInfinity;

            // v = sqrt(2 * a * s) — потолок, с которого ещё успеваем остановиться за Brake Time.
            float brakeCap = float.IsPositiveInfinity(brakeRate)
                ? maxSpeed
                : Mathf.Sqrt(2f * brakeRate * Mathf.Max(0f, remaining));
            float target = Mathf.Min(maxSpeed, brakeCap);
            float rate = target + 1e-4f >= _currentSpeed ? accelRate : brakeRate;
            return Mathf.MoveTowards(_currentSpeed, target, rate * deltaTime);
        }

        private void UpdateTilt(float deltaTime)
        {
            // k = ω / v, поэтому v²·k = v·ω. Плюс — поворот вправо.
            // ω с path, а не с кадра: на прямом куске ломаной yaw корпуса стоит, и кадр давал бы 0.
            float yawRate = PathYawRate();
            float lateralAcceleration = _currentSpeed * yawRate;
            float targetTilt = Mathf.Atan2(lateralAcceleration, Physics.gravity.magnitude) * Mathf.Rad2Deg * _tiltStrength;
            targetTilt = Mathf.Clamp(targetTilt, -_maxTilt, _maxTilt);
            _currentTilt = Mathf.Lerp(_currentTilt, targetTilt, _tiltSmooth * deltaTime);
        }

        private float PathYawRate()
        {
            if (_currentSpeed <= 1e-4f || _route == null || _pathLength <= 1e-3f)
                return 0f;

            int index = FindTurn(_pathProgress);
            if (index >= 0)
            {
                PathTurn turn = _turns[index];
                float length = Mathf.Max(0.05f, turn.End - turn.Start);
                return turn.SignedAngle * Mathf.Deg2Rad / length * _currentSpeed;
            }

            const float Window = 2f;
            float from = Mathf.Clamp(_pathProgress - Window * 0.5f, 0f, _pathLength);
            float to = Mathf.Clamp(_pathProgress + Window * 0.5f, 0f, _pathLength);
            float span = to - from;
            if (span < 0.05f)
                return 0f;

            float deltaYaw = Mathf.DeltaAngle(TangentYaw(from), TangentYaw(to));
            return deltaYaw * Mathf.Deg2Rad / span * _currentSpeed;
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
            int current = FindTurn(_pathProgress);
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

        private bool TryBuildRoute(FMVector3[] fMVector3)
        {
            if (!NavigationAI.TryBuildCurve(WithBodyStart(fMVector3), out Vector3[] curve))
                return false;

            // Сэмпл Безье может на долю миллиметра уехать от контрола. Первая точка — тело, иначе первый Tick сдвинет объект.
            curve[0] = transform.position;
            _route = curve;
            _routeDistance = new float[curve.Length];
            float length = 0f;
            for (int i = 1; i < curve.Length; i++)
            {
                length += Vector3.Distance(curve[i - 1], curve[i]);
                _routeDistance[i] = length;
            }

            _pathLength = length;
            if (length < 1e-4f)
                return false;

            BuildTurns();
            return true;
        }

        private FMVector3[] WithBodyStart(FMVector3[] controls)
        {
            if (controls == null || controls.Length == 0)
                return controls;

            Vector3 body = transform.position;
            FMVector3 first = controls[0];
            float dx = body.x - first.X;
            float dy = body.y - first.Y;
            float dz = body.z - first.Z;
            // Уже на первой точке. Второй такой же контрол даёт Безье нулевой отрезок.
            if (dx * dx + dy * dy + dz * dz <= 1e-6f)
                return controls;

            var withStart = new FMVector3[controls.Length + 1];
            withStart[0] = new FMVector3(body.x, body.y, body.z);
            for (int i = 0; i < controls.Length; i++)
                withStart[i + 1] = controls[i];

            return withStart;
        }

        private void BuildTurns()
        {
            _turns = System.Array.Empty<PathTurn>();
            if (_route == null || _route.Length < 2)
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

            while (traveled < _pathLength - 1e-4f)
            {
                float next = Mathf.Min(traveled + Step, _pathLength);
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
                AddTurn(turns, start, _pathLength, accum);

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
            if (!TrySample(distance, out _, out Vector3 tangent))
                return transform.eulerAngles.y;

            tangent.y = 0f;
            if (tangent.sqrMagnitude < 1e-8f)
                return transform.eulerAngles.y;

            return Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
        }

        private bool TrySample(float distance, out Vector3 position, out Vector3 tangent)
        {
            position = default;
            tangent = default;
            if (_route == null || _routeDistance == null || _route.Length < 2)
                return false;

            int last = _route.Length - 1;
            if (distance <= 0f)
            {
                position = _route[0];
                tangent = _route[1] - _route[0];
                return true;
            }

            if (distance >= _routeDistance[last])
            {
                position = _route[last];
                tangent = _route[last] - _route[last - 1];
                return true;
            }

            int index = 1;
            while (index < last && _routeDistance[index] < distance)
                index++;

            float span = _routeDistance[index] - _routeDistance[index - 1];
            float along = span > 1e-5f ? (distance - _routeDistance[index - 1]) / span : 0f;
            position = Vector3.Lerp(_route[index - 1], _route[index], along);
            tangent = _route[index] - _route[index - 1];
            return true;
        }

        private float SignedYawAlongPath()
        {
            if (!TrySample(_pathProgress, out _, out Vector3 tangent))
                return 0f;

            Vector3 flat = tangent;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-8f)
                return 0f;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-8f)
                return 0f;

            return Vector3.SignedAngle(forward, flat, Vector3.up);
        }

        private void ApplyAnim()
        {
            EnsureAnim();
            bool turn = _isMoving && !_isStopping && _turnSide != 0;
            bool stop = _isMoving && _isStopping;
            bool move = _isMoving && !_isStopping && !turn;
            SetAnim(ref _moveAnim, move);
            SetAnim(ref _stopAnim, stop);
            SetAnim(ref _leftAnim, turn && _turnSide < 0);
            SetAnim(ref _rightAnim, turn && _turnSide > 0);
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
            _moveAnim = Bind(MoveParam);
            _stopAnim = Bind(StopParam);
            _leftAnim = Bind(LeftParam);
            _rightAnim = Bind(RightParam);
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

        private void SetAnim(ref AnimTrigger slot, bool active)
        {
            if (_animator == null || !slot.Exists || slot.Armed == active)
                return;

            slot.Armed = active;
            if (active)
                _animator.SetTrigger(slot.Hash);
            else
                _animator.ResetTrigger(slot.Hash);
        }

        private void OnDisable()
        {
            _isMoving = false;
            _isStopping = false;
            _turnSide = 0;
            _latchedTurn = -1;
            _alignLatched = false;
            ApplyAnim();
        }

        private void ApplyPose(float deltaTime)
        {
            if (!TrySample(_pathProgress, out Vector3 position, out Vector3 tangent))
                return;

            transform.position = position;
            if (!_faceMoveDirection)
                return;

            Vector3 flat = tangent;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-8f)
                return;

            Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
            if (_turnSpeed <= 0f)
            {
                transform.rotation = look;
                return;
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _turnSpeed * deltaTime);
        }

        private void Arrive()
        {
            if (_route != null && _route.Length > 0)
                transform.position = _route[_route.Length - 1];
            _pathProgress = _pathLength;
            _currentSpeed = 0f;
            _isMoving = false;
            _isStopping = false;
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