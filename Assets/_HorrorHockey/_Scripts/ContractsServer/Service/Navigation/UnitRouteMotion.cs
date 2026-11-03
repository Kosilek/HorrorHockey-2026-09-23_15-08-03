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

        public bool HasRoute => _route != null && _route.Length >= 2;

        #region Fields Unit
        private float _maxSpeed;
        private float _accelTime;
        private float _brakeTime;

        private readonly float _curveStep = GameConstants.CurveStep;
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
            _route = null;
            _routeDistance = null;
            PathLength = 0f;
        }
        #endregion

        /// <summary>
        /// <para>{EN} Build the curve from the unit start through the controls. Y is world Z.</para>
        /// {RU} Строит кривую от текущей точки юнита через контролы. Y — мировая Z.
        /// </summary>
        public bool TryStart(FMVector2 start, FMVector2[] controls)
        {
            PathProgress = 0f;
            CurrentSpeed = 0f;
            IsMoving = false;
            Position = start;
            _route = null;
            _routeDistance = null;
            PathLength = 0f;

            if (!TryBuildRoute(start, controls, _curveStep))
                return false;

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
            CurrentSpeed = StepSpeed(remaining, deltaTime, _maxSpeed, _accelTime, _brakeTime);
            float moveDist = CurrentSpeed * deltaTime;
            if (moveDist >= remaining)
            {
                PathProgress = PathLength;
                CurrentSpeed = 0f;
                SampleAt(PathProgress);
                IsMoving = false;
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
            if (!IsMoving || deltaTime <= 0f)
                return;

            float remaining = PathLength - PathProgress;
            if (remaining <= 1e-4f)
            {
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

        private void SampleAt(float distance)
        {
            var pos = Position;
            if (TrySample(distance, out FMVector2 position, out _))
                Position = position;
            Debug.DrawLine(new Vector3(pos.X, 0f, pos.Y), new Vector3(Position.X, 0f, Position.Y), Color.red, 15f);
        }

        private float StepSpeed(float remaining, float deltaTime, float maxSpeed, float accelTime, float brakeTime)
        {
            maxSpeed = Max(0f, maxSpeed);
            float accelRate = accelTime > 1e-4f ? maxSpeed / accelTime : float.PositiveInfinity;
            float brakeRate = brakeTime > 1e-4f ? maxSpeed / brakeTime : float.PositiveInfinity;

            // v = sqrt(2 * a * s) — потолок, с которого ещё успеваем остановиться за Brake Time.
            float brakeCap = float.IsPositiveInfinity(brakeRate)
                ? maxSpeed
                : Sqrt(2f * brakeRate * Max(0f, remaining));
            float target = Min(maxSpeed, brakeCap);
            float rate = target + 1e-4f >= CurrentSpeed ? accelRate : brakeRate;
            return MoveTowards(CurrentSpeed, target, rate * deltaTime);
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
