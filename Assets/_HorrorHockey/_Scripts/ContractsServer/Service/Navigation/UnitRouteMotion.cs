using ContractsServer.Data;
using UnityEngine;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace ContractsServer.Service.Navigation
{
    /// <summary>
    /// <para>{EN} Position along a route. Same curve, accel and brake as the client body. No pose, tilt or animation.</para>
    /// {RU} Позиция вдоль маршрута. Та же кривая, разгон и тормоз, что у тела на клиенте. Без позы, наклона и анимации.
    /// </summary>
    public sealed class UnitRouteMotion
    {
        private FMVector2[] _route;
        private float[] _routeDistance;

        public bool IsMoving { get; private set; }
        public float CurrentSpeed { get; private set; }
        public float PathProgress { get; private set; }
        public float PathLength { get; private set; }
        public FMVector2 Position { get; private set; }

        /// <summary>
        /// <para>{EN} False while this route keeps its speed through the last point.</para>
        /// {RU} false, пока этот маршрут не гасит скорость на последней точке.
        /// </summary>
        public bool StopsAtEnd { get; private set; } = true;

        public bool HasRoute => _route != null && _route.Length >= 2;

        /// <summary>
        /// <para>{EN} True while paused. Speed is zero, the route and progress are kept.</para>
        /// {RU} true на паузе. Скорость ноль, маршрут и пройденный путь сохраняются.
        /// </summary>
        public bool IsPaused { get; private set; }

        #region Fields Unit
        private float _maxSpeed;
        public float MaxSpeed => _maxSpeed;
        private float _accelTime;
        private float _brakeTime;

        private readonly float _curveStep = GameConstants.CurveStep;
        #endregion

        #region Fields Slow
        private const float SlowRecoverAccelTimes = 2f;

        private float _slowTimeLeft;
        private float _slowRecoverRate;
        #endregion

        #region Action Fields
        public void Initialize(
            float maxSpeed,
            float accelTime,
            float brakeTime,
            FMVector2 position)
        {
            _maxSpeed = maxSpeed;
            _accelTime = accelTime;
            _brakeTime = brakeTime;
            SetPosition(position);
        }

        private void SetPosition(FMVector2 position)
        {
            Position = position;
            PathProgress = 0f;
            CurrentSpeed = 0f;
            IsMoving = false;
            IsPaused = false;
            _slowTimeLeft = 0f;
            _slowRecoverRate = 0f;
            _route = null;
            _routeDistance = null;
            PathLength = 0f;
        }
        #endregion

        /// <summary>
        /// <para>{EN} Drops the speed to this value at once. Then it climbs back to max over two accel times. A new call restarts the recovery from the new speed.</para>
        /// {RU} Сразу опускает скорость до этого значения. Затем она возвращается к максимуму за два времени разгона. Новый вызов перезапускает восстановление от новой скорости.
        /// </summary>
        public void SlowTo(float speed)
        {
            float maxSpeed = Max(0f, _maxSpeed);
            CurrentSpeed = Min(Max(0f, speed), maxSpeed);
            _slowTimeLeft = SlowRecoverAccelTimes * _accelTime;
            _slowRecoverRate = _slowTimeLeft > 1e-4f ? (maxSpeed - CurrentSpeed) / _slowTimeLeft : 0f;
            if (_slowRecoverRate <= 0f)
                _slowTimeLeft = 0f;
        }

        /// <summary>
        /// <para>{EN} Stops at once and holds the place. The route stays.</para>
        /// {RU} Сразу останавливает и держит на месте. Маршрут остаётся.
        /// </summary>
        public void Pause()
        {
            IsPaused = true;
            CurrentSpeed = 0f;
        }

        /// <summary>
        /// <para>{EN} Continues the same route with the usual accel from zero.</para>
        /// {RU} Продолжает тот же маршрут обычным разгоном с нуля.
        /// </summary>
        public void Resume()
        {
            IsPaused = false;
        }

        /// <summary>
        /// <para>{EN} Build the curve from the unit start through the controls. Brakes to a stop at the end. Y is world Z.</para>
        /// {RU} Строит кривую от текущей точки юнита через контролы. В конце тормозит до остановки. Y — мировая Z.
        /// </summary>
        public bool TryStart(FMVector2 start, FMVector2[] controls)
        {
            return TryStart(start, controls, stopAtEnd: true);
        }

        /// <summary>
        /// <para>{EN} Same curve as TryStart. With stopAtEnd false the unit keeps its speed on the last point.</para>
        /// {RU} Та же кривая, что у TryStart. При stopAtEnd false юнит не гасит скорость на последней точке.
        /// </summary>
        public bool TryStart(FMVector2 start, FMVector2[] controls, bool stopAtEnd)
        {
            return TryStart(start, controls, stopAtEnd, startSpeed: 0f);
        }

        /// <summary>
        /// <para>{EN} Same curve as TryStart, but the unit enters it at startSpeed instead of accelerating from zero.</para>
        /// {RU} Та же кривая, что у TryStart, но юнит входит в неё со скоростью startSpeed, а не разгоняется с нуля.
        /// </summary>
        public bool TryStart(FMVector2 start, FMVector2[] controls, bool stopAtEnd, float startSpeed)
        {
            PathProgress = 0f;
            CurrentSpeed = 0f;
            IsMoving = false;
            StopsAtEnd = stopAtEnd;
            Position = start;
            _route = null;
            _routeDistance = null;
            PathLength = 0f;

            if (!TryBuildRoute(start, controls, _curveStep))
                return false;

            CurrentSpeed = IsPaused ? 0f : Min(Max(0f, startSpeed), Max(0f, _maxSpeed));
            IsMoving = true;
            SampleAt(0f);
            return true;
        }

        public void Stop()
        {
            IsMoving = false;
            CurrentSpeed = 0f;
            _route = null;
            _routeDistance = null;
        }

        /// <summary>
        /// <para>{EN} Snap to the end and stop. Used when the leftover distance is already gone.</para>
        /// {RU} Ставит в конец и останавливает. Когда остаток пути уже меньше порога.
        /// </summary>
        public void Complete()
        {
            if (HasRoute)
                SampleAt(PathLength);
            PathProgress = PathLength;
            CurrentSpeed = 0f;
            IsMoving = false;
            _route = null;
            _routeDistance = null;
        }

        /// <summary>
        /// <para>{EN} One speed step. True when this step lands on the end.</para>
        /// {RU} Один шаг скорости. true, если этот шаг доходит до конца.
        /// </summary>
        public bool Advance(float deltaTime)
        {
            float remaining = PathLength - PathProgress;
            float brakeTime = StopsAtEnd ? _brakeTime : 0f;
            CurrentSpeed = StepSpeed(CurrentSpeed, _slowTimeLeft, remaining, deltaTime, _maxSpeed, _accelTime, brakeTime);
            if (_slowTimeLeft > 0f)
                _slowTimeLeft = Max(0f, _slowTimeLeft - deltaTime);
            float moveDist = CurrentSpeed * deltaTime;
            if (moveDist >= remaining)
            {
                PathProgress = PathLength;
                SampleAt(PathProgress);
                if (StopsAtEnd)
                {
                    CurrentSpeed = 0f;
                    IsMoving = false;
                }

                return true;
            }

            PathProgress += moveDist;
            SampleAt(PathProgress);
            return false;
        }

        /// <summary>
        /// <para>{EN} Full position tick: early stop, then one advance. Pose is not applied here.</para>
        /// {RU} Полный тик позиции: ранний финиш, затем один шаг. Позу здесь не ставит.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!IsMoving || IsPaused || deltaTime <= 0f)
                return;

            float remaining = PathLength - PathProgress;
            if (remaining <= 1e-4f)
            {
                if (StopsAtEnd)
                    Complete();
                return;
            }

            Advance(deltaTime);
        }

        public bool TrySample(float distance, out FMVector2 position, out FMVector2 tangent)
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
            position = Lerp(_route[index - 1], _route[index], along);
            tangent = _route[index] - _route[index - 1];
            return true;
        }

        private bool TryBuildRoute(FMVector2 start, FMVector2[] controls, float curveStep)
        {
            if (!RouteCurve.TryBuild(WithStart(start, controls), curveStep, out FMVector2[] curve))
                return false;

            // Сэмпл Безье может на долю миллиметра уехать от контрола. Первая точка — тело.
            curve[0] = start;
            _route = curve;
            _routeDistance = new float[curve.Length];
            float length = 0f;
            for (int i = 1; i < curve.Length; i++)
            {
                length += Distance(curve[i - 1], curve[i]);
                _routeDistance[i] = length;
            }

            PathLength = length;
            return length >= 1e-4f;
        }

        private static FMVector2[] WithStart(FMVector2 start, FMVector2[] controls)
        {
            if (controls == null || controls.Length == 0)
                return controls;

            FMVector2 first = controls[0];
            float dx = start.X - first.X;
            float dz = start.Y - first.Y;
            // Уже на первой точке. Второй такой же контрол даёт Безье нулевой отрезок.
            if (dx * dx + dz * dz <= 1e-6f)
                return controls;

            var withStart = new FMVector2[controls.Length + 1];
            withStart[0] = start;
            for (int i = 0; i < controls.Length; i++)
                withStart[i + 1] = controls[i];

            return withStart;
        }

        #region Contact
        /// <summary>
        /// <para>{EN} First touch of two round bodies while both keep their current routes. Both run ahead on copies of their state
        /// with the same accel, brake, slow and pause, so nothing here changes. Point lies on the line between centers, on the edge of b.</para>
        /// {RU} Первое касание двух круглых тел, если оба продолжат свои текущие маршруты. Оба прогоняются вперёд на копии состояния
        /// с тем же разгоном, тормозом, замедлением и паузой, поэтому здесь ничего не меняется. Точка — на линии между центрами, на краю b.
        /// </summary>
        /// <param name="radiusA">{EN} Body radius of a. {RU} Радиус тела a.</param>
        /// <param name="frozenA">{EN} a stands still whatever its route, e.g. attacking. {RU} a стоит на месте независимо от маршрута, например в атаке.</param>
        /// <param name="horizon">{EN} How far ahead to look, seconds. {RU} На сколько секунд вперёд смотреть.</param>
        /// <param name="step">{EN} Simulation step, seconds. Pass the server tick for the exact match. {RU} Шаг прогона, с. Для точного совпадения — тик сервера.</param>
        /// <param name="time">{EN} Seconds from now until the touch. {RU} Секунд от текущего момента до касания.</param>
        public static bool TryFindContact(
            UnitRouteMotion a, float radiusA, bool frozenA,
            UnitRouteMotion b, float radiusB, bool frozenB,
            float horizon, float step,
            out float time, out FMVector2 point)
        {
            return TryFindContact(a, radiusA, frozenA, b, radiusB, frozenB, horizon, step, out time, out point, out _);
        }

        /// <summary>
        /// <para>{EN} Same as above, plus where the center of a stands at the touch. Head a there to meet b edge to edge, not center to center.</para>
        /// {RU} То же, что выше, плюс где стоит центр a в момент касания. Веди a туда, чтобы встретить b краями, а не центрами.
        /// </summary>
        /// <param name="centerA">{EN} Center of a at the touch, radiusA + radiusB away from the center of b. {RU} Центр a в момент касания, на расстоянии radiusA + radiusB от центра b.</param>
        public static bool TryFindContact(
            UnitRouteMotion a, float radiusA, bool frozenA,
            UnitRouteMotion b, float radiusB, bool frozenB,
            float horizon, float step,
            out float time, out FMVector2 point, out FMVector2 centerA)
        {
            time = 0f;
            point = default;
            centerA = default;
            if (a == null || b == null || step <= 1e-4f)
                return false;

            float radius = radiusA + radiusB;
            if (radius <= 0f)
                return false;

            float radiusSqr = radius * radius;
            float shareB = radiusB / radius;
            ContactProbe probeA = a.CreateProbe(frozenA);
            ContactProbe probeB = b.CreateProbe(frozenB);
            if (DistanceSqr(probeA.Position, probeB.Position) <= radiusSqr)
            {
                point = Lerp(probeB.Position, probeA.Position, shareB);
                centerA = probeA.Position;
                return true;
            }

            float elapsed = 0f;
            while (elapsed < horizon)
            {
                if (!probeA.Moving && !probeB.Moving)
                    return false;

                float deltaTime = Min(step, horizon - elapsed);
                FMVector2 fromA = probeA.Position;
                FMVector2 fromB = probeB.Position;
                a.StepProbe(ref probeA, deltaTime);
                b.StepProbe(ref probeB, deltaTime);

                // Внутри шага движение прямое: ищем вход в касание на отрезке, чтобы быстрые тела не проскочили друг сквозь друга.
                if (TryEnterOnStep(fromA, probeA.Position, fromB, probeB.Position, radiusSqr, out float along))
                {
                    FMVector2 touchA = Lerp(fromA, probeA.Position, along);
                    FMVector2 touchB = Lerp(fromB, probeB.Position, along);
                    time = elapsed + deltaTime * along;
                    point = Lerp(touchB, touchA, shareB);
                    centerA = touchA;
                    return true;
                }

                elapsed += deltaTime;
            }

            return false;
        }

        /// <summary>
        /// <para>{EN} Touch of two round bodies within one tick. Bodies that already touch count only while they keep closing in,
        /// so a unit can drive away from the one it stopped at.</para>
        /// {RU} Касание двух круглых тел в пределах одного тика. Уже касающиеся тела засчитываются, только пока продолжают сближаться,
        /// поэтому юнит может отъехать от того, о кого остановился.
        /// </summary>
        /// <param name="time">{EN} Seconds from the tick start until the touch, 0 if they already touch. {RU} Секунд от начала тика до касания, 0 если уже касаются.</param>
        /// <param name="centerA">{EN} Center of a at the touch. {RU} Центр a в момент касания.</param>
        /// <param name="centerB">{EN} Center of b at the touch. {RU} Центр b в момент касания.</param>
        public static bool TryFindHit(
            UnitRouteMotion a, float radiusA, bool frozenA,
            UnitRouteMotion b, float radiusB, bool frozenB,
            float deltaTime, out float time, out FMVector2 centerA, out FMVector2 centerB)
        {
            time = 0f;
            centerA = default;
            centerB = default;
            if (a == null || b == null || deltaTime <= 1e-4f)
                return false;

            float radius = radiusA + radiusB;
            if (radius <= 0f)
                return false;

            ContactProbe probeA = a.CreateProbe(frozenA);
            ContactProbe probeB = b.CreateProbe(frozenB);
            if (!probeA.Moving && !probeB.Moving)
                return false;

            FMVector2 fromA = probeA.Position;
            FMVector2 fromB = probeB.Position;
            a.StepProbe(ref probeA, deltaTime);
            b.StepProbe(ref probeB, deltaTime);

            float radiusSqr = radius * radius;
            float startSqr = DistanceSqr(fromA, fromB);
            if (startSqr <= radiusSqr)
            {
                centerA = fromA;
                centerB = fromB;
                return DistanceSqr(probeA.Position, probeB.Position) < startSqr - 1e-6f;
            }

            if (!TryEnterOnStep(fromA, probeA.Position, fromB, probeB.Position, radiusSqr, out float along))
                return false;

            time = deltaTime * along;
            centerA = Lerp(fromA, probeA.Position, along);
            centerB = Lerp(fromB, probeB.Position, along);
            return true;
        }

        private ContactProbe CreateProbe(bool frozen)
        {
            return new ContactProbe
            {
                Moving = !frozen && IsMoving && !IsPaused && HasRoute,
                Speed = CurrentSpeed,
                Progress = PathProgress,
                SlowTimeLeft = _slowTimeLeft,
                Position = Position
            };
        }

        private void StepProbe(ref ContactProbe probe, float deltaTime)
        {
            if (!probe.Moving)
                return;

            float remaining = PathLength - probe.Progress;
            if (remaining <= 1e-4f)
            {
                probe.Moving = false;
                return;
            }

            float brakeTime = StopsAtEnd ? _brakeTime : 0f;
            probe.Speed = StepSpeed(probe.Speed, probe.SlowTimeLeft, remaining, deltaTime, _maxSpeed, _accelTime, brakeTime);
            if (probe.SlowTimeLeft > 0f)
                probe.SlowTimeLeft = Max(0f, probe.SlowTimeLeft - deltaTime);

            probe.Progress = Min(PathLength, probe.Progress + probe.Speed * deltaTime);
            if (TrySample(probe.Progress, out FMVector2 position, out _))
                probe.Position = position;
        }

        private static bool TryEnterOnStep(FMVector2 fromA, FMVector2 toA, FMVector2 fromB, FMVector2 toB, float radiusSqr, out float along)
        {
            along = 0f;
            float dx = fromA.X - fromB.X;
            float dy = fromA.Y - fromB.Y;
            float vx = (toA.X - fromA.X) - (toB.X - fromB.X);
            float vy = (toA.Y - fromA.Y) - (toB.Y - fromB.Y);

            // |d + v·t|² = R²  →  a·t² + b·t + c = 0, t в [0, 1]
            float a = vx * vx + vy * vy;
            float b = 2f * (dx * vx + dy * vy);
            float c = dx * dx + dy * dy - radiusSqr;
            if (a <= 1e-8f || b >= 0f)
                return false;

            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
                return false;

            float t = (-b - Sqrt(discriminant)) / (2f * a);
            if (t < 0f || t > 1f)
                return false;

            along = t;
            return true;
        }

        private static float DistanceSqr(FMVector2 a, FMVector2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private struct ContactProbe
        {
            public bool Moving;
            public float Speed;
            public float Progress;
            public float SlowTimeLeft;
            public FMVector2 Position;
        }
        #endregion

        private void SampleAt(float distance)
        {
            var pos = Position;
            if (TrySample(distance, out FMVector2 position, out _))
                Position = position;
            Debug.DrawLine(new Vector3(pos.X, 0f, pos.Y), new Vector3(Position.X, 0f, Position.Y), Color.red, 15f);
        }

        private float StepSpeed(float speed, float slowTimeLeft, float remaining, float deltaTime, float maxSpeed, float accelTime, float brakeTime)
        {
            maxSpeed = Max(0f, maxSpeed);
            float accelRate = accelTime > 1e-4f ? maxSpeed / accelTime : float.PositiveInfinity;
            float brakeRate = brakeTime > 1e-4f ? maxSpeed / brakeTime : float.PositiveInfinity;

            // v = sqrt(2 * a * s) — потолок, с которого ещё успеваем остановиться за Brake Time.
            float brakeCap = float.IsPositiveInfinity(brakeRate)
                ? maxSpeed
                : Sqrt(2f * brakeRate * Max(0f, remaining));
            float target = Min(maxSpeed, brakeCap);
            float rate = target + 1e-4f >= speed
                ? (slowTimeLeft > 0f ? _slowRecoverRate : accelRate)
                : brakeRate;
            return MoveTowards(speed, target, rate * deltaTime);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Abs(target - current) <= maxDelta)
                return target;

            return current + (target >= current ? 1f : -1f) * maxDelta;
        }

        private static FMVector2 Lerp(FMVector2 a, FMVector2 b, float t)
        {
            if (t < 0f)
                t = 0f;
            else if (t > 1f)
                t = 1f;

            return new FMVector2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        private static float Distance(FMVector2 a, FMVector2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return Sqrt(dx * dx + dy * dy);
        }

        private static float Abs(float value) => value < 0f ? -value : value;
        private static float Max(float a, float b) => a > b ? a : b;
        private static float Min(float a, float b) => a < b ? a : b;
        private static float Sqrt(float value) => System.MathF.Sqrt(value);
    }
}
