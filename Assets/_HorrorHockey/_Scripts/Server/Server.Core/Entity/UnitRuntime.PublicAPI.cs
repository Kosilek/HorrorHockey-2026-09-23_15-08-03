using ContractsServer.Command;
using ContractsServer.Data;
using ContractsServer.Service.Navigation;
using Server.Core.Enums;
using System;
using System.Diagnostics;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace Server.Core.Entity
{
    public partial class UnitRuntime
    {
        private const float _defaultHorizon = 0.2f;
        private const float _approachOverlap = 0.01f;

        private bool _ignoreContacts = false;
        private float _cooldownContacts = 0f;
        private const float __cooldownContactsTime = 2f;

        private FMVector2 savePositionTarget;

        // Первая атака по цели на бегу — чардж: юнит не встаёт, а доезжает до удара. Сбросится ударом или потерей скорости.
        private bool _attackCharge;

        #region Public API

        public void Tick(float deltaTime)
        {
            if (_unitAggression.StatusUnit == StatusUnit.Die)
                return;

            if (CanMoveByRoute && !IsStunned)
            {
                if (_routeMotion.HasRoute)
                {
                    _routeMotion.Tick(deltaTime);
                    if (IsCircling && _routeMotion.PathLength - _routeMotion.PathProgress <= 1e-4f)
                        FinishCircle();
                    if (_attackCharge && !IsRunning())
                        _attackCharge = false;
                }
                else if (_followBaseRoute)
                {
                    if (TryBeginRoute(_baseRoute))
                    {
                        _sendCommand.Invoke(CommandWs.CreateRoute, (Id, RouteStart, RouteControls));
                    }
                }
            }

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
                    TickMoveTarget();
                    break;
                case 1:

                    break;
                case 2:
                    if (CanRebuildRoute && _unitAggression.StatusUnit is not (StatusUnit.Attack or StatusUnit.MoveTarget))
                    {
                        if (_unitAggression.CheckTarget(CurrentCell))
                        {
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
                    TickMoveTarget();
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

        private void TickMoveTarget()
        {
            if (_unitAggression.StatusUnit != StatusUnit.MoveTarget || !CanRebuildRoute)
                return;

            UnitRuntime target = _unitAggression.CurrentTarget;
            if (target == null || savePositionTarget == target.Position)
                return;

            _unitAggression.CheckDistanceToTarget(Position);
            StartAttackCharge();

            var controls = new FMVector2[1];
            controls[0] = GetApproachPoint(target);
            savePositionTarget = target.Position;
            SteerToTarget(controls, target);
        }

        // Погоня только выбирает, куда ехать. Само столкновение ловит MatchController для всех юнитов.
        private void SteerToTarget(FMVector2[] controls, UnitRuntime target)
        {
            if (IsRunning() && TryFindMovingContact(target, _defaultHorizon, out FMVector2 selfCenter, out _))
                controls[0] = selfCenter;

            TryStartMovingRoute(controls);
        }

        // Конец маршрута погони — не центр цели, а точка на сумме радиусов от него со стороны юнита.
        // Чуть внутрь, чтобы касание засчиталось, когда юнит встанет в конце маршрута.
        private FMVector2 GetApproachPoint(UnitRuntime target)
        {
            FMVector2 from = Position;
            FMVector2 to = target.Position;
            float dx = from.X - to.X;
            float dy = from.Y - to.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            float gap = MathF.Max(0f, (BodyWidth + target.BodyWidth) * 0.5f - _approachOverlap);
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
            UnityEngine.Debug.Log("Start Cicle");
            if (!TryGetForward(out FMVector2 forward))
                return false;

            return TryStartCircle(radius, forward, laps, onStart, onComplete, clockwise);
        }

        private bool TryStartCircle(float radius, FMVector2 forward, int laps = 1, Action onStart = null, Action onComplete = null, bool clockwise = true)
        {
            UnityEngine.Debug.Log($"Validation Cicle unit {Id} forward ({forward.X:0.00}, {forward.Y:0.00}) clockwise {clockwise}");

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
                UnityEngine.Debug.Log($"Validation Cicle 2 false kept: {kept}");
                return false; }
            UnityEngine.Debug.Log("Validation Cicle 2");
            if (kept < waypoints.Length)
                Array.Resize(ref waypoints, kept);

            if (!_navigationService.TryBuildRoute(waypoints, out FMVector2[] controls))
               { return false; }
            UnityEngine.Debug.Log("Validation Cicle 3");
            if (!TryBeginRoute(controls, stopAtEnd: false, _routeMotion.CurrentSpeed))
                { return false; }
            UnityEngine.Debug.Log("Validation Cicle 4");

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

            float speed = MathF.Max(_routeMotion.CurrentSpeed, _routeMotion.MaxSpeed * 0.5f);
            float reach = speed * GameConstants.DeltaTime * ticks;
            if (reach <= 1e-3f)
                return false;

            if (width <= 0f)
                width = BodyWidth;
            rounding = MathF.Min(MathF.Max(rounding, 0.05f), 0.5f);

            // Справа от курса — (forward.Y, -forward.X), если смотреть сверху.
            float sideX = turnRight ? forward.Y : -forward.Y;
            float sideY = turnRight ? -forward.X : forward.X;
            FMVector2 start = Position;
            var tip = new FMVector2(start.X + forward.X * reach, start.Y + forward.Y * reach);
            var end = new FMVector2(start.X + sideX * width, start.Y + sideY * width);

            // Кривая через одну вершину дала бы иглу или петлю. Срезаем угол точками на обеих сторонах и вершиной, подтянутой к ним.
            FMVector2 cutIn = Lerp(tip, start, rounding);
            FMVector2 cutOut = Lerp(tip, end, rounding);
            FMVector2 apex = Lerp(tip, Lerp(cutIn, cutOut, 0.5f), 0.5f);
            if (!_navigationService.IsWalkable(apex) || !_navigationService.IsWalkable(end))
                return false;

            var waypoints = new[] { start, cutIn, apex, cutOut, end };
            if (!_navigationService.TryBuildRoute(waypoints, out FMVector2[] controls))
                return false;
            if (!TryBeginRoute(controls, stopAtEnd: false, _routeMotion.CurrentSpeed))
                return false;

            _followBaseRoute = false;
            _circleControls = controls;
            _circleComplete = onComplete;
            _sendCommand.Invoke(CommandWs.CreateRoute, (Id, RouteStart, controls, false, _routeMotion.CurrentSpeed));
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
            UnityEngine.Debug.Log("DropOrders");
            ResetCircle();
            _followBaseRoute = false;
            _routeMotion.Stop();
            Stun();
            _sendCommand.Invoke(CommandWs.StopUnit, Id);
        }

        private void FinishCircle()
        {
            Action onComplete = _circleComplete;
            ResetCircle();
            // onComplete может сразу начать следующий манёвр с текущей скоростью и курсом, поэтому полоса — только если не начал.
            onComplete?.Invoke();
            if (!IsCircling)
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
