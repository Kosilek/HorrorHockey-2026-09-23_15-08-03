using ContractsServer.Service.Navigation;
using Server.Core.Enums;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace Server.Core.Entity
{
    public partial class UnitRuntime
    {
        private UnitRuntime _contactWith;
        private float _contactTime;
        private bool _contactRollsFall;

        // Курс и сторона круга берутся при планировании: к разбору тела уже проехали друг сквозь друга.
        private bool _contactHasForward;
        private FMVector2 _contactForward;
        private bool _contactClockwise;

        // Пара, чей контакт уже разобран. Пока тела не разошлись, новое касание той же пары не планируется.
        private UnitRuntime _contactPartner;

        // Исход броска падения по этой паре. Считается один раз: второй юнит читает готовое, а не бросает заново.
        private bool _fallReady;
        private UnitRuntime _fallPartner;
        private bool _fell;

        /// <summary>
        /// <para>{EN} True while the body moves this tick by its route.</para>
        /// {RU} true, пока тело в этом тике едет по маршруту.
        /// </summary>
        // Маршрут без торможения в конце держит скорость и IsMoving, хотя тело уже стоит на последней точке.
        public bool IsBodyMoving => _routeMotion.IsMoving && !_routeMotion.IsPaused && CanMoveByRoute && !IsStunned
            && _routeMotion.PathProgress < _routeMotion.PathLength - 1e-4f;

        /// <summary>
        /// <para>{EN} Runs at half of max speed or faster. Whoever it touches rolls for a fall.</para>
        /// {RU} Бежит со скоростью не ниже половины максимальной. Тот, кого он коснётся, бросает шанс на падение.
        /// </summary>
        public bool IsCharging => IsBodyMoving && CurrentSpeed >= _routeMotion.MaxSpeed * 0.5f;

        public bool IsDead => _unitAggression.StatusUnit == StatusUnit.Die;

        /// <summary>
        /// <para>{EN} Touch with the other body during the next tick, if both keep their routes.</para>
        /// {RU} Касание с другим телом за следующий тик, если оба продолжат свои маршруты.
        /// </summary>
        /// <param name="time">{EN} Seconds from now. {RU} Секунд от текущего момента.</param>
        /// <param name="center">{EN} Center of this unit at the touch. {RU} Центр этого юнита в момент касания.</param>
        /// <param name="otherCenter">{EN} Center of the other unit at the touch. {RU} Центр другого юнита в момент касания.</param>
        public bool TryFindHit(UnitRuntime other, float deltaTime, out float time, out FMVector2 center, out FMVector2 otherCenter)
        {
            time = 0f;
            center = default;
            otherCenter = default;
            if (other == null || ReferenceEquals(other, this))
                return false;

            return UnitRouteMotion.TryFindHit(
                _routeMotion, BodyWidth * 0.5f, !CanMoveByRoute,
                other._routeMotion, other.BodyWidth * 0.5f, !other.CanMoveByRoute,
                deltaTime, out time, out center, out otherCenter);
        }

        /// <summary>
        /// <para>{EN} Plans the charge touch for the next tick. The earliest one wins. A moving unit gets a short route that ends at the touch,
        /// so the client stops there too and does not overshoot.</para>
        /// {RU} Планирует касание-чардж на следующий тик. Побеждает самое раннее. Едущий юнит получает короткий маршрут, кончающийся в точке касания,
        /// поэтому клиент тоже встаёт там и не проезжает дальше.
        /// </summary>
        /// <param name="rollFall">{EN} The other one was charging, so this one rolls for a fall. {RU} Другой бежал, поэтому этот бросает шанс на падение.</param>
        /// <param name="center">{EN} Center of this unit at the touch. {RU} Центр этого юнита в момент касания.</param>
        public void PlanContact(UnitRuntime other, float time, bool rollFall, FMVector2 center)
        {
            if (_contactWith != null && _contactTime <= time)
                return;

            _contactWith = other;
            _contactTime = time;
            _contactRollsFall = rollFall;
            _fallReady = false;
            _contactHasForward = TryGetForward(out _contactForward);
            _contactClockwise = IsHitFromLeft(other);

            if (!IsBodyMoving)
                return;

            //// Касание прямо сейчас: маршрут нулевой длины не строится, поэтому встаём на месте.
            //if (!TryStartMovingRoute(new[] { center }))
            //    DropOrders();
        }

        /// <summary>
        /// <para>{EN} After the move of the planned tick. A fall drops orders. Whoever stays up circles only when the other one fell. The pair is closed until the bodies separate.</para>
        /// {RU} После движения запланированного тика. Падение сбрасывает приказы. Устоявший идёт по кругу, только если упал второй. Пара закрыта, пока тела не разошлись.
        /// </summary>
        public void ResolveContact()
        {
            if (_ignoreContacts)
                return;
            UnitRuntime other = _contactWith;
            _contactWith = null;
            if (other == null)
                return;

            _ignoreContacts = true;
            _attackCharge = false;
            _chaseToAttackRange = false;
            _contactPartner = other;
            if (!_contactRollsFall)
                return;

            // otherFell — упал ли второй от этой же пары. Круг только у того, кто сам устоял, и только если упал второй.
            bool isFall = RollFall(other, out bool otherFell);
            if (isFall)
                Stun(StunTime);
            else if (otherFell)
            {
                if (_contactHasForward)
                    TryStartCircle(2.5f, _contactForward, onComplete: TurnBackThenEngage, clockwise: _contactClockwise);
                else
                    TryStartCircle(2.5f, onComplete: TurnBackThenEngage, clockwise: _contactClockwise);
            }
        }

        // Разворот может не построиться у стены — тогда на цель идём сразу с конца круга.
        private void TurnBackThenEngage()
        {
            if (!TryStartTurnBack(onComplete: () => EngageTarget()))
                EngageTarget();
        }



        // Удар слева уводит круг вправо, то есть по часовой: центр круга всегда с дальней от другого стороны.
        // Почти лобовой удар решается одинаково для обоих в паре — одно направление на встречных курсах даёт восьмёрку.
        private bool IsHitFromLeft(UnitRuntime other)
        {
            const float headOnSin = 0.1f;
            bool pairSide = ((Id < other.Id ? Id : other.Id) & 1) == 0;
            if (!_contactHasForward)
                return pairSide;

            float dx = other.Position.X - Position.X;
            float dy = other.Position.Y - Position.Y;
            float distance = System.MathF.Sqrt(dx * dx + dy * dy);
            if (distance <= 1e-5f)
                return pairSide;

            float cross = (_contactForward.X * dy - _contactForward.Y * dx) / distance;
            if (System.MathF.Abs(cross) < headOnSin)
                return pairSide;

            return cross > 0f;
        }

        /// <summary>
        /// <para>{EN} True while this contact is still closed with the other unit.</para>
        /// {RU} true, пока этот контакт с другим юнитом ещё закрыт.
        /// </summary>
        public bool HasContactPartner(UnitRuntime other)
        {
            return other != null && ReferenceEquals(_contactPartner, other);
        }

        /// <summary>
        /// <para>{EN} Opens the closed contact once the other body is gone or the bodies no longer touch.</para>
        /// {RU} Открывает закрытый контакт, когда другого тела нет или тела больше не касаются.
        /// </summary>
        public void ReleaseContactPartner()
        {
            if (_contactPartner == null || _contactPartner.IsDead || !Overlaps(_contactPartner))
                _contactPartner = null;
        }

        private bool Overlaps(UnitRuntime other)
        {
            float dx = Position.X - other.Position.X;
            float dy = Position.Y - other.Position.Y;
            float radius = (BodyWidth + other.BodyWidth) * 0.5f;
            return dx * dx + dy * dy <= radius * radius;
        }

        // Бросок этого юнита и исход второго по той же паре. Второй ещё не разобран — его бросок считается здесь и сохраняется ему.
        private bool RollFall(UnitRuntime other, out bool otherFell)
        {
            bool isFall = RollOwnFall(other);
            otherFell = other.PeekFall(this);
            return isFall;
        }

        private bool RollOwnFall(UnitRuntime partner)
        {
            if (_fallReady && ReferenceEquals(_fallPartner, partner))
                return _fell;

            return StoreFall(partner);
        }

        // true, если этот юнит падает от контакта с partner. Чужой контакт и отсутствие броска оставляют его на ногах.
        private bool PeekFall(UnitRuntime partner)
        {
            if (_fallReady && ReferenceEquals(_fallPartner, partner))
                return _fell;

            // Игнор закрывает новые контакты. Уже посчитанный исход читается веткой выше:
            // свой ResolveContact включает его до того, как партнёр спросит результат.
            if (_ignoreContacts)
                return false;

            if (_contactRollsFall && ReferenceEquals(_contactWith, partner))
                return StoreFall(partner);

            return false;
        }

        private bool StoreFall(UnitRuntime partner)
        {
            _fallPartner = partner;
            _fallReady = true;

            float totalMass = Mass + partner.Mass;
            if (totalMass <= 0f)
            {
                _fell = false;
                return false;
            }

            _fell = _random.NextDouble() < partner.Mass / totalMass;
            return _fell;
        }
    }
}
