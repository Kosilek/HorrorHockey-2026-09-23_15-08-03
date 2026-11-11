using ContractsServer.Command;
using ContractsServer.Data;
using ContractsServer.Service.Navigation;
using Server.Core.Enums;
using System;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace Server.Core.Entity
{
    public partial class UnitRuntime
    {
        private const float _defaultHorizon = 0.2f;
        private const float _defaultTick = GameConstants.DeltaTime;
        private const float _approachOverlap = 0.01f;
        // Конец погони чуть внутри радиуса атаки, чтобы проверка дистанции точно засчитала его.
        private const float _attackRangeInset = 0.05f;

        private bool _ignoreContacts = false;
        private float _cooldownContacts = 0f;
        private const float __cooldownContactsTime = 4f;

        private FMVector2 savePositionTarget;

        // Первая атака по цели на бегу — чардж: юнит не встаёт, а доезжает до удара. Сбросится ударом или потерей скорости.
        private bool _attackCharge;

        // Погоня после манёвра встаёт на радиусе атаки, а не едет до касания.
        private bool _chaseToAttackRange;

        #region Public API

        public void Tick(float deltaTime)
        {
            if (_unitAggression.StatusUnit == StatusUnit.Die)
                return;

            if (_stunTimer > 0f)
            {
                _stunTimer -= deltaTime;
                if (_stunTimer <= 0f)
                {
                    _stunTimer = 0f;
                    Unstun();
                }
            }

            // Встал бить посреди маршрута: маршрут гасится. Иначе он заморожен со скоростью бега
            // и при выходе из атаки сразу продолжится чарджем, хотя юнит стоял.
            if (_unitAggression.StatusUnit == StatusUnit.Attack && !CanMoveByRoute && !IsStunned && _routeMotion.IsMoving)
                HoldPosition();

            if (CanMoveByRoute && !IsStunned)
            {
                if (_routeMotion.HasRoute)
                {
                    _routeMotion.Tick(deltaTime);
                    if (IsCircling && IsCircleEnding(deltaTime))
                        FinishCircle();
                    if (_attackCharge && !IsRunning())
                        _attackCharge = false;
                }
                else if (_followBaseRoute)
                {
                    if (TryBeginRoute(_baseRoute))
                    {
                        _unitAggression.SetStatusUnit(StatusUnit.Move);
                        _sendCommand.Invoke(CommandWs.CreateRoute, (Id, RouteStart, RouteControls));
                    }
                }
            }

            ClipLoopAttack();

            if (_ignoreContacts)
            {
                _cooldownContacts += 0.2f;

                if (_cooldownContacts >= __cooldownContactsTime)
                {
                    _cooldownContacts = 0f;
                    _ignoreContacts = false;
                }
            }

            // Столкновения ищут соседей по клетке каждый тик, поэтому клетка тоже обновляется каждый тик.
            CurrentCell = _funcUpdateCell(Position, Id, CurrentCell);

            switch (_currentTick)
            {
                case 0:
                    AggresiveTick();
                    break;
                case 1:

                    break;
                case 2:
                    if (CanRebuildRoute && _unitAggression.StatusUnit is not (StatusUnit.Attack or StatusUnit.MoveTarget))
                    {
                        if (_unitAggression.CheckTarget(CurrentCell))
                        {
                            _chaseToAttackRange = false;
                            StartAttackCharge();
                            FMVector2[] controls = new FMVector2[1];
                            controls[0] = GetApproachPoint(_unitAggression.CurrentTarget);
                            savePositionTarget = _unitAggression.CurrentTarget.Position;
                            if (_unitAggression.StatusUnit == StatusUnit.Idle)
                                TryStartRoute(controls);
                            else
                                SteerToTarget(controls, _unitAggression.CurrentTarget);
                        }
                    }
                    break;
                case 3:
                    AggresiveTick();
                    break;
                case 4:

                    break;
            }

            _currentTick++;
            if (_currentTick > _maxTick)
            {
                _currentTick = 0;
            }
        }

        private void AggresiveTick()
        {
            TickMoveTarget();
            TickAttack();
        }

        private void TickMoveTarget()
        {
            if (_unitAggression.StatusUnit != StatusUnit.MoveTarget || !CanRebuildRoute)
                return;

            if (!_unitAggression.HasTarget)
            {
                ResetTarget();
                return;
            }

            // Дистанция проверяется каждый раз: к стоящей цели юнит доезжает без перестройки маршрута и должен перейти в атаку.
            UnitRuntime target = _unitAggression.CurrentTarget;
            _unitAggression.CheckDistanceToTarget(Position);
            if (_chaseToAttackRange)
                _attackCharge = false;
            else
                StartAttackCharge();
            if (savePositionTarget == target.Position)
                return;

            if (_chaseToAttackRange)
            {
                TryStartAttackRangeRoute(target);
                return;
            }

            // Чардж едет до касания, а не в слот, поэтому места у цели не держит.
            ClearAttackSlot();
            var controls = new FMVector2[1];
            controls[0] = GetApproachPoint(target);
            savePositionTarget = target.Position;
            SteerToTarget(controls, target);
        }

        private void TickAttack()
        {
            if (_unitAggression.StatusUnit == StatusUnit.Attack)
            {
                if (_attackCharge || IsCircling || IsDrivingToAttackRange)
                    return;

                // Между ударами: цель могла умереть или отойти, тогда атака на месте бессмысленна.
                if (!_isStartAttackLoop)
                {
                    if (!_unitAggression.HasTarget)
                    {
                        ResetTarget();
                        return;
                    }

                    _unitAggression.UpdateStatusByDistance(Position);
                    if (_unitAggression.StatusUnit == StatusUnit.MoveTarget)
                    {
                        // Сброс заставит TickMoveTarget построить погоню. Цель отошла от стоящего бойца —
                        // он доезжает до места для атаки, а не разгоняется в чардж.
                        savePositionTarget = default;
                        _chaseToAttackRange = true;
                        return;
                    }
                }

                StartAttackLoop();
            }
        }

        // Погоня только выбирает, куда ехать. Само столкновение ловит MatchController для всех юнитов.
        private bool SteerToTarget(FMVector2[] controls, UnitRuntime target)
        {
            if (IsRunning() && TryFindMovingContact(target, _defaultHorizon, out FMVector2 selfCenter, out _))
                controls[0] = selfCenter;

            return TryStartMovingRoute(controls);
        }

        #region Target
        public StatusUnit Status => _unitAggression.StatusUnit;
        public UnitRuntime CurrentTarget => _unitAggression.CurrentTarget;

        /// <summary>
        /// <para>{EN} True while the target is set and alive.</para>
        /// {RU} true, пока цель задана и жива.
        /// </summary>
        public bool HasTarget => _unitAggression.HasTarget;

        /// <summary>
        /// <para>{EN} Every status change of this unit: unit, previous status, new status.</para>
        /// {RU} Каждая смена статуса этого юнита: юнит, прежний статус, новый статус.
        /// </summary>
        public event Action<UnitRuntime, StatusUnit, StatusUnit> StatusChanged;

        /// <summary>
        /// <para>{EN} Goes for the current target right now without a charge: attacks in place inside the attack radius,
        /// otherwise drives to the attack radius, brakes there and attacks. The chase keeps stopping at the attack radius
        /// until the target is reset, a new one is found or a contact happens. Without a live target the target is reset.
        /// False when nothing started: no target, dead, stunned or in a maneuver.</para>
        /// {RU} Сразу идёт на текущую цель без чарджа: в радиусе атаки бьёт на месте, иначе доезжает до радиуса атаки,
        /// тормозит там и бьёт. Погоня и дальше встаёт на радиусе атаки, пока цель не сброшена, не найдена новая или не случился контакт.
        /// Без живой цели цель сбрасывается. false, если ничего не началось: нет цели, мёртв, в стане или в манёвре.
        /// </summary>
        public bool EngageTarget()
        {
            if (IsDead || !CanRebuildRoute)
                return false;

            if (!_unitAggression.HasTarget)
            {
                ResetTarget(returnToLane: false);
                return false;
            }

            _attackCharge = false;
            UnitRuntime target = _unitAggression.CurrentTarget;
            _unitAggression.UpdateStatusByDistance(Position);
            if (_unitAggression.StatusUnit == StatusUnit.Attack && IsFreeAttackSpot(target, Position))
            {
                AttackInPlace(target);
                return true;
            }

            _chaseToAttackRange = TryStartAttackRangeRoute(target);
            if (_chaseToAttackRange)
                return true;

            // Слот совпал с текущим местом или маршрут не построился: в радиусе бьём отсюда.
            if (_unitAggression.StatusUnit != StatusUnit.Attack)
                return false;

            AttackInPlace(target);
            return true;
        }

        private void AttackInPlace(UnitRuntime target)
        {
            _chaseToAttackRange = false;
            ClaimAttackSlot(target, Position);
            StartAttackLoop();
        }

        /// <summary>
        /// <para>{EN} Forgets the target: Idle, no charge, the attack clip stops. With returnToLane the unit goes back to the lane loop, if it may rebuild the route now.</para>
        /// {RU} Забывает цель: Idle, без чарджа, клип атаки останавливается. При returnToLane юнит возвращается к циклу по полосе, если сейчас может перестроить маршрут.
        /// </summary>
        public void ResetTarget(bool returnToLane = true)
        {
            _unitAggression.ClearTarget();
            _attackCharge = false;
            _chaseToAttackRange = false;
            ClearAttackSlot();
            StopAttackClip();
            savePositionTarget = default;

            if (returnToLane && CanRebuildRoute && !IsDead)
                FollowBaseRoute();
        }

        private void OnStatusChanged(StatusUnit previous, StatusUnit status)
        {
            ReleaseTargetOnDeath(status);
            StatusChanged?.Invoke(this, previous, status);
        }

        // Едет в слот вокруг цели и тормозит там, а не проезжает его с текущей скоростью.
        private bool TryStartAttackRangeRoute(UnitRuntime target)
        {
            FMVector2 slot = FindAttackSlot(target);
            float dx = slot.X - Position.X;
            float dy = slot.Y - Position.Y;
            if (dx * dx + dy * dy <= _slotTolerance * _slotTolerance)
                return false;

            FMVector2[] controls = BuildSlotControls(target, slot);
            if (!TryBeginRoute(controls, stopAtEnd: true, _routeMotion.CurrentSpeed))
                return false;

            // Удары по пути к слоту не идут: петля атаки перезапустится, когда юнит встанет.
            StopAttackClip();
            ClaimAttackSlot(target, slot);
            savePositionTarget = target.Position;
            _followBaseRoute = false;
            _sendCommand.Invoke(CommandWs.CreateRoute, (Id, RouteStart, controls, true, _routeMotion.CurrentSpeed));
            return true;
        }
        #endregion

        // Конец маршрута погони — не центр цели, а точка на сумме радиусов от него со стороны юнита.
        // Чуть внутрь, чтобы касание засчиталось, когда юнит встанет в конце маршрута.
        private FMVector2 GetApproachPoint(UnitRuntime target)
        {
            return GetApproachPoint(target, (BodyWidth + target.BodyWidth) * 0.5f - _approachOverlap);
        }

        // Точка на расстоянии gap от центра цели со стороны юнита.
        private FMVector2 GetApproachPoint(UnitRuntime target, float gap)
        {
            FMVector2 from = Position;
            FMVector2 to = target.Position;
            float dx = from.X - to.X;
            float dy = from.Y - to.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            gap = MathF.Max(0f, gap);
            if (distance <= gap || distance <= 1e-5f)
                return from;

            float scale = gap / distance;
            return new FMVector2(to.X + dx * scale, to.Y + dy * scale);
        }

        // Зовётся сразу после того, как агрессия могла перевести юнита в атаку. До этого юнит в атаке не был.
        private void StartAttackCharge()
        {
            _attackCharge = _unitAggression.StatusUnit == StatusUnit.Attack && IsRunning();
        }

        private bool IsRunning()
        {
            return _routeMotion.IsMoving
                && _routeMotion.PathProgress < _routeMotion.PathLength - 1e-4f
                && CurrentSpeed >= _routeMotion.MaxSpeed / 2f;
        }

        /// <summary>
        /// <para>{EN} Leaves the lane and follows these controls from the current position. Stops at the end.</para>
        /// {RU} Сходит с полосы и идёт по этим контролам от текущей позиции. В конце останавливается.
        /// </summary>
        public bool TryStartRoute(FMVector2[] controls)
        {
            if (!TryBeginRoute(controls))
                return false;

            _followBaseRoute = false;
            _sendCommand.Invoke(CommandWs.CreateRoute, (Id, RouteStart, controls));
            return true;
        }

        /// <summary>
        /// <para>{EN} Leaves the lane and follows these controls from the current position. Keeps the current speed and does not brake at the end.</para>
        /// {RU} Сходит с полосы и идёт по этим контролам от текущей позиции. Сохраняет текущую скорость и в конце не тормозит.
        /// </summary>
        public bool TryStartMovingRoute(FMVector2[] controls)
        {
            float startSpeed = _routeMotion.CurrentSpeed;
            if (!TryBeginRoute(controls, stopAtEnd: false, startSpeed))
                return false;

            _followBaseRoute = false;
            _sendCommand.Invoke(CommandWs.CreateRoute, (Id, RouteStart, controls, false, _routeMotion.CurrentSpeed));
            return true;
        }

        /// <summary>
        /// <para>{EN} Back to the lane loop from the current position.</para>
        /// {RU} Возврат к циклу по полосе от текущей позиции.
        /// </summary>
        public void FollowBaseRoute()
        {
            _followBaseRoute = true;
            _routeMotion.Stop();
        }

        /// <summary>
        /// <para>{EN} Circles around a center placed to the side of the unit at radius distance: to the right when clockwise, to the left otherwise.
        /// The unit already stands on the circle and leaves it along its current heading.
        /// Same curve, speed and obstacle bypass as any other route. Aggression does not rebuild the route until the circle ends.
        /// At the end returns to the lane loop and calls onComplete.</para>
        /// {RU} Круг вокруг центра сбоку от юнита на расстоянии радиуса: справа по часовой, слева против. Юнит уже стоит на окружности и уходит по ней вдоль текущего курса.
        /// Та же кривая, скорость и обход препятствий, что у любого маршрута. Агрессия не перестраивает маршрут, пока круг не закончится.
        /// В конце возвращается к циклу по полосе и вызывает onComplete.
        /// </summary>
        /// <param name="laps">{EN} Full turns, at least one. {RU} Полных оборотов, минимум один.</param>
        /// <param name="clockwise">{EN} Seen from above. {RU} Если смотреть сверху.</param>
        public bool TryStartCircle(float radius, int laps = 1, Action onStart = null, Action onComplete = null, bool clockwise = true)
        {
            //if (radius <= 1e-3f || laps < 1 || _navigationService == null)
            //    return false;
            if (!TryGetForward(out FMVector2 forward))
                return false;

            return TryStartCircle(radius, forward, laps, onStart, onComplete, clockwise);
        }

        private bool TryStartCircle(float radius, FMVector2 forward, int laps = 1, Action onStart = null, Action onComplete = null, bool clockwise = true)
        {

            FMVector2 position = Position;
            // Справа от курса — (forward.Y, -forward.X), если смотреть сверху.
            float side = clockwise ? radius : -radius;
            float centerX = position.X + forward.Y * side;
            float centerY = position.Y - forward.X * side;
            float startAngle = MathF.Atan2(position.Y - centerY, position.X - centerX);
            float angleStep = (clockwise ? -2f : 2f) * MathF.PI / CirclePointsPerLap;

            int count = laps * CirclePointsPerLap;
            var waypoints = new FMVector2[count + 1];
            waypoints[0] = position;
            int kept = 1;
            for (int i = 1; i < count; i++)
            {
                float angle = startAngle + angleStep * i;
                var point = new FMVector2(centerX + MathF.Cos(angle) * radius, centerY + MathF.Sin(angle) * radius);
                // Обход сохраняет вейпоинты как есть, поэтому точку внутри препятствия выкидываем, а обход ведёт хорду вокруг.
                if (_navigationService.IsWalkable(point))
                    waypoints[kept++] = point;
            }
            waypoints[kept++] = position;

            if (kept < 3)
                {
                return false; }
            if (kept < waypoints.Length)
                Array.Resize(ref waypoints, kept);

            if (!_navigationService.TryBuildRoute(waypoints, out FMVector2[] controls))
               { return false; }
            if (!TryBeginRoute(controls, stopAtEnd: false, _routeMotion.CurrentSpeed))
                { return false; }

            _followBaseRoute = false;
            _circleControls = controls;
            _circleComplete = onComplete;
            _sendCommand.Invoke(CommandWs.CreateRoute, (Id, RouteStart, controls, false, _routeMotion.CurrentSpeed));
            onStart?.Invoke();
            return true;
        }

        /// <summary>
        /// <para>{EN} Runs ahead for a few ticks, turns back and returns past the start, shifted to the side by width.
        /// The path is a sharp triangle with a rounded tip. Works as a circle: aggression and contacts wait, StopCircle ends it,
        /// at the end returns to the lane loop and calls onComplete.</para>
        /// {RU} Проезжает вперёд на несколько тиков, разворачивается и возвращается к старту со сдвигом вбок на width.
        /// Путь — острый треугольник со скруглённой вершиной. Работает как круг: агрессия и контакты ждут, StopCircle прерывает,
        /// в конце возврат к циклу по полосе и вызов onComplete.
        /// </summary>
        /// <param name="ticks">{EN} How far ahead, in server ticks at the current speed, at least half of max. {RU} Насколько вперёд — в тиках сервера при текущей скорости, не ниже половины максимальной.</param>
        /// <param name="turnRight">{EN} Side of the turn, seen from above. {RU} Сторона разворота, если смотреть сверху.</param>
        /// <param name="width">{EN} Side shift of the way back. 0 — body width. {RU} Сдвиг обратного пути вбок. 0 — ширина тела.</param>
        /// <param name="rounding">{EN} Share of each side cut by the rounded tip, 0.05..0.5. {RU} Доля каждой стороны, которую срезает скругление вершины, 0.05..0.5.</param>
        public bool TryStartTurnBack(float ticks = 2f, bool turnRight = true, float width = 0f, float rounding = 0.3f,
            Action onStart = null, Action onComplete = null)
        {
            if (ticks <= 0f || _navigationService == null || !TryGetForward(out FMVector2 forward))
                return false;

            if (width <= 0f)
                width = BodyWidth;

            // Справа от курса — (forward.Y, -forward.X), если смотреть сверху.
            float sideX = turnRight ? forward.Y : -forward.Y;
            float sideY = turnRight ? -forward.X : forward.X;
            FMVector2 start = Position;
            var end = new FMVector2(start.X + sideX * width, start.Y + sideY * width);
            return TryStartTurnBack(forward, TurnBackReach(ticks), end, rounding, stopAtEnd: false, onStart, onComplete);
        }

        // Вперёд на столько тиков при текущей скорости, не ниже половины максимальной.
        private float TurnBackReach(float ticks)
        {
            float speed = MathF.Max(_routeMotion.CurrentSpeed, _routeMotion.MaxSpeed * 0.5f);
            return speed * GameConstants.DeltaTime * ticks;
        }

        // Треугольник со скруглённой вершиной: от текущей позиции вперёд на reach и обратно в end.
        private bool TryStartTurnBack(FMVector2 forward, float reach, FMVector2 end, float rounding, bool stopAtEnd,
            Action onStart, Action onComplete)
        {
            if (reach <= 1e-3f || _navigationService == null)
                return false;

            rounding = MathF.Min(MathF.Max(rounding, 0.05f), 0.5f);
            FMVector2 start = Position;
            var tip = new FMVector2(start.X + forward.X * reach, start.Y + forward.Y * reach);

            // Кривая через одну вершину дала бы иглу или петлю. Срезаем угол точками на обеих сторонах и вершиной, подтянутой к ним.
            FMVector2 cutIn = Lerp(tip, start, rounding);
            FMVector2 cutOut = Lerp(tip, end, rounding);
            FMVector2 apex = Lerp(tip, Lerp(cutIn, cutOut, 0.5f), 0.5f);
            if (!_navigationService.IsWalkable(apex) || !_navigationService.IsWalkable(end))
                return false;

            var waypoints = new[] { start, cutIn, apex, cutOut, end };
            if (!_navigationService.TryBuildRoute(waypoints, out FMVector2[] controls))
                return false;
            if (!TryBeginRoute(controls, stopAtEnd, _routeMotion.CurrentSpeed))
                return false;

            _followBaseRoute = false;
            _circleControls = controls;
            _circleComplete = onComplete;
            _sendCommand.Invoke(CommandWs.CreateRoute, (Id, RouteStart, controls, stopAtEnd, _routeMotion.CurrentSpeed));
            onStart?.Invoke();
            return true;
        }

        private static FMVector2 Lerp(FMVector2 from, FMVector2 to, float t)
        {
            return new FMVector2(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);
        }

        /// <summary>
        /// <para>{EN} Ends the circle early from the current position and returns to the lane loop. onComplete is not called.</para>
        /// {RU} Досрочно завершает круг с текущей позиции и возвращает к циклу по полосе. onComplete не вызывается.
        /// </summary>
        public void StopCircle()
        {
            if (!IsCircling)
                return;

            ResetCircle();
            FollowBaseRoute();
        }

        /// <summary>
        /// <para>{EN} Tells the client to start an animation clip on this unit.</para>
        /// {RU} Сообщает клиенту, что у этого юнита нужно запустить клип анимации.
        /// </summary>
        public void StartAnimationClip(byte animationClip)
        {
            _sendCommand.Invoke(CommandWs.StartAnimationClip, ((int)Id, animationClip));
        }

        /// <summary>
        /// <para>{EN} Drops the speed by amount at once. It climbs back to max over two accel times. Stacks with a slow already running.</para>
        /// {RU} Сразу снижает скорость на amount. Она возвращается к максимуму за два времени разгона. Складывается с уже действующим замедлением.
        /// </summary>
        public void SlowDown(float amount)
        {
            if (amount <= 0f)
                return;

            _routeMotion.SlowTo(_routeMotion.CurrentSpeed - amount);
            _sendCommand.Invoke(CommandWs.SlowUnit, (Id, _routeMotion.CurrentSpeed));
        }

        /// <summary>
        /// <para>{EN} Stops at once and holds the current route. Each Stun needs its own Unstun.</para>
        /// {RU} Сразу останавливает и держит текущий маршрут. На каждый Stun нужен свой Unstun.
        /// </summary>
        public void Stun()
        {
            if (_stunCount++ > 0)
                return;

            _routeMotion.Pause();
            _sendCommand.Invoke(CommandWs.StunUnit, Id);
        }

        /// <summary>
        /// <para>{EN} Stun that lifts itself after duration seconds. A new timed stun while one is running does not stack, it only extends the time to the longer one.</para>
        /// {RU} Стан, который сам снимается через duration секунд. Новый стан по времени поверх текущего не складывается, а только продлевает время до большего.
        /// </summary>
        public void Stun(float duration)
        {
            if (duration <= 0f)
                return;

            if (_stunTimer > 0f)
            {
                _stunTimer = MathF.Max(_stunTimer, duration);
                return;
            }

            _stunTimer = duration;
            Stun();
        }

        /// <summary>
        /// <para>{EN} Lifts one Stun. After the last one the unit continues its route with the usual accel.
        /// With keepRoute false the route, the circle and the lane loop are dropped, so the unit stands without orders.</para>
        /// {RU} Снимает один Stun. После последнего юнит продолжает свой маршрут обычным разгоном.
        /// При keepRoute false маршрут, круг и цикл по полосе сбрасываются, и юнит стоит без приказа.
        /// </summary>
        public void Unstun(bool keepRoute = true)
        {
            if (_stunCount == 0)
                return;

            if (!keepRoute)
                DropOrders();

            if (--_stunCount > 0)
                return;

            _stunTimer = 0f;
            _routeMotion.Resume();
            _sendCommand.Invoke(CommandWs.UnstunUnit, Id);
        }

        /// <summary>
        /// <para>{EN} Where and when this unit first touches the other one if both keep their current routes.
        /// Counts accel, brake, slow and stun. A unit in attack stands still. Bodies are circles of BodyWidth.</para>
        /// {RU} Где и когда этот юнит впервые коснётся другого, если оба продолжат свои текущие маршруты.
        /// Учитывает разгон, торможение, замедление и стан. Юнит в атаке стоит на месте. Тела — круги диаметром BodyWidth.
        /// </summary>
        /// <param name="other">{EN} The second unit. {RU} Второй юнит.</param>
        /// <param name="horizon">{EN} How far ahead to look, seconds. {RU} На сколько секунд вперёд смотреть.</param>
        /// <param name="point">{EN} Touch point on the edge of other. {RU} Точка касания на краю other.</param>
        /// <param name="time">{EN} Seconds from now until the touch, 0 if already touching. {RU} Секунд до касания, 0 если уже касаются.</param>
        public bool TryFindContact(UnitRuntime other, float horizon, out FMVector2 point, out float time)
        {
            point = default;
            time = 0f;
            if (other == null || ReferenceEquals(other, this))
                return false;

            return UnitRouteMotion.TryFindContact(
                _routeMotion, BodyWidth * 0.5f, !CanMoveByRoute,
                other._routeMotion, other.BodyWidth * 0.5f, !other.CanMoveByRoute,
                horizon, GameConstants.DeltaTime,
                out time, out point);
        }
        #endregion

        // selfCenter — где будет центр этого юнита в момент касания, туда и ведём маршрут, чтобы тела встретились краями.
        private bool TryFindMovingContact(UnitRuntime other, float horizon, out FMVector2 selfCenter, out float time)
        {
            selfCenter = default;
            time = 0f;
            if (other == null || ReferenceEquals(other, this))
                return false;

            return UnitRouteMotion.TryFindContact(
                _routeMotion, BodyWidth * 0.5f, false,
                other._routeMotion, other.BodyWidth * 0.5f, !other.CanMoveByRoute,
                horizon, GameConstants.DeltaTime,
                out time, out _, out selfCenter);
        }

        private void DropOrders()
        {
            ResetCircle();
            _followBaseRoute = false;
            _routeMotion.Stop();
            Stun();
            _sendCommand.Invoke(CommandWs.StopUnit, Id);
        }

        // Манёвр без торможения кончится в следующем тике: следующий запускается сейчас. Иначе остаток тика упрётся
        // в последнюю точку, и тело на сервере и на клиенте постоит там, пока не придёт новый маршрут.
        private bool IsCircleEnding(float deltaTime)
        {
            float remaining = _routeMotion.PathLength - _routeMotion.PathProgress;
            if (remaining <= 1e-4f)
                return true;

            return !_routeMotion.StopsAtEnd && remaining <= _routeMotion.CurrentSpeed * deltaTime;
        }

        private void FinishCircle()
        {
            Action onComplete = _circleComplete;
            FMVector2[] finished = RouteControls;
            ResetCircle();
            // onComplete может сразу начать следующий манёвр или погоню, поэтому полоса — только если маршрут не сменился.
            // Юнит с целью после манёвра бьёт с места, а не уходит на полосу.
            onComplete?.Invoke();
            if (ReferenceEquals(RouteControls, finished) && !_unitAggression.HasTarget)
                FollowBaseRoute();
        }

        private void ResetCircle()
        {
            _circleControls = null;
            _circleComplete = null;
            // Агрессия перестраивает погоню только при смене позиции цели. Сброс заставит её перестроить после круга.
            savePositionTarget = default;
        }

        private bool TryGetForward(out FMVector2 forward)
        {
            forward = default;
            FMVector2 tangent;
            if (_routeMotion.HasRoute)
            {
                if (!_routeMotion.TrySample(_routeMotion.PathProgress, out _, out tangent))
                    return false;
            }
            else if (RouteControls != null && RouteControls.Length > 0)
            {
                int last = RouteControls.Length - 1;
                FMVector2 from = last > 0 ? RouteControls[last - 1] : RouteStart;
                tangent = RouteControls[last] - from;
            }
            else
            {
                return false;
            }

            float length = MathF.Sqrt(tangent.X * tangent.X + tangent.Y * tangent.Y);
            if (length <= 1e-5f)
                return false;

            forward = new FMVector2(tangent.X / length, tangent.Y / length);
            return true;
        }

        private bool TryBeginRoute(FMVector2[] controls, bool stopAtEnd = true, float startSpeed = 0f)
        {
            FMVector2 start = _routeMotion.Position;
            if (!_routeMotion.TryStart(start, controls, stopAtEnd, startSpeed))
                return false;

            RouteStart = start;
            RouteControls = controls;
            return true;
        }
    }
}
