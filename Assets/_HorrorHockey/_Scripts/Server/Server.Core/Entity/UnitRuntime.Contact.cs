using ContractsServer.Command;
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

        // Касание пришлось в юнита, который уже в драке: оно только бросает падение и не трогает его маршрут и бой.
        private bool _contactKeepsOrders;

        // Упал от касания. Встав, идёт драться со своей целью.
        private bool _fallen;

        // Курс и сторона круга берутся при планировании: к разбору тела уже проехали друг сквозь друга.
        private bool _contactHasForward;
        private FMVector2 _contactForward;
        private bool _contactClockwise;

        // Шёл ли юнит по полосе до обрезки маршрута касанием. После касания без цели он туда вернётся.
        private bool _contactResumeLane;

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
        /// <para>{EN} Runs at half of max speed or faster. Whoever it touches rolls for a fall. Driving to the place for an attack is not a charge.</para>
        /// {RU} Бежит со скоростью не ниже половины максимальной. Тот, кого он коснётся, бросает шанс на падение. Доезд до места для атаки — не чардж.
        /// </summary>
        public bool IsCharging => IsBodyMoving && !IsDrivingToAttackRange && CurrentSpeed >= _routeMotion.MaxSpeed * 0.5f;

        public bool IsDead => _unitAggression.StatusUnit == StatusUnit.Die;

        /// <summary>
        /// <para>{EN} Already in a fight: just had a contact, lies after a fall or makes the maneuver after a hit.
        /// Knocks nobody down itself. A hit into it only rolls its fall and does not change its orders.</para>
        /// {RU} Уже в драке: только что был контакт, лежит после падения или в манёвре после удара.
        /// Сам никого не роняет. Удар в него только бросает ему падение и не меняет его приказов.
        /// </summary>
        public bool IsInFight => _ignoreContacts || IsStunned || IsCircling;

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

            bool first = _contactWith == null;
            _contactWith = other;
            _contactTime = time;
            _contactRollsFall = rollFall;
            _fallReady = false;

            // Юнит в драке продолжает свой круг или бой: маршрут обрезается только тому, кто в него врезался.
            _contactKeepsOrders = IsInFight;
            if (_contactKeepsOrders)
                return;

            if (first)
                _contactResumeLane = _followBaseRoute;
            _contactHasForward = TryGetForward(out _contactForward);
            _contactClockwise = IsHitFromLeft(other);

            if (!IsBodyMoving)
                return;

            // Без обрезки оба тела проедут весь следующий тик и к разбору уже будут друг в друге.
            if (!TryStartMovingRoute(new[] { center }))
                HoldPosition();
        }

        // Касание прямо сейчас: маршрут нулевой длины не строится, поэтому встаём на месте без стана.
        private void HoldPosition()
        {
            ResetCircle();
            _followBaseRoute = false;
            _routeMotion.Stop();
            _sendCommand.Invoke(CommandWs.StopUnit, Id);
        }

        /// <summary>
        /// <para>{EN} After the move of the planned tick. A fall drops orders. Whoever stays up circles when the other one fell from this hit or was already down,
        /// otherwise stops to fight. A unit already in a fight only rolls its fall. The pair is closed until the bodies separate.</para>
        /// {RU} После движения запланированного тика. Падение сбрасывает приказы. Устоявший идёт по кругу, если второй упал от этого удара или уже лежал,
        /// иначе встаёт драться. Юнит, который уже был в драке, только бросает падение. Пара закрыта, пока тела не разошлись.
        /// </summary>
        public void ResolveContact()
        {
            UnitRuntime other = _contactWith;
            _contactWith = null;
            if (other == null)
                return;

            _contactPartner = other;
            _ignoreContacts = true;
            _cooldownContacts = 0f;

            // Поиск цели на круге и лёжа не идёт: без этого юнит, сбитый на полосе, после круга уехал бы дальше по ней.
            if (!_unitAggression.HasTarget && !other.IsDead && other.Team != Team)
                _unitAggression.SetTargetUnit(other, Position);

            if (_contactKeepsOrders)
            {
                if (_contactRollsFall && RollOwnFall(other))
                    Fall();
                return;
            }

            _attackCharge = false;
            _chaseToAttackRange = false;
            ClearAttackSlot();

            if (_contactRollsFall && RollOwnFall(other))
            {
                Fall();
                return;
            }

            if ((other.PeekFall(this) || other.IsStunned) && StartContactCircle())
                return;

            ContinueAfterContact();
        }

        // Упавший бросает круг, удары и маршрут: лёжа он не бьёт и не доезжает манёвр. Встав, идёт на свою цель.
        private void Fall()
        {
            _attackCharge = false;
            _chaseToAttackRange = false;
            ClearAttackSlot();
            StopAttackClip();
            HoldPosition();
            _fallen = true;
            Stun(StunTime);
            StartAnimationClip((byte)StateAnimation.Fall);

            if (!IsStunned)
                GetUp();
        }

        // Подъезжает к месту для атаки и бьёт, без чарджа. Без цели возвращается на полосу, если шёл по ней.
        private void GetUp()
        {
            if (!_fallen)
                return;

            _fallen = false;
            ContinueAfterContact();
        }

        private bool StartContactCircle()
        {
            if (_contactHasForward)
                return TryStartCircle(2.5f, _contactForward, onComplete: TurnBackThenEngage, clockwise: _contactClockwise);

            return TryStartCircle(2.5f, onComplete: TurnBackThenEngage, clockwise: _contactClockwise);
        }

        // Маршрут обрезан точкой касания, поэтому без продолжения юнит так и стоял бы в ней.
        // С целью встаёт на место для атаки, без цели возвращается на полосу, если шёл по ней.
        private void ContinueAfterContact()
        {
            if (EngageTarget())
                return;

            if (_contactResumeLane && CanRebuildRoute && !IsDead)
                FollowBaseRoute();
        }

        // Разворот может не построиться у стены — тогда на цель идём сразу с конца круга.
        // Сдвиг обратного пути — в ту же сторону, что и круг, то есть от второго юнита.
        private void TurnBackThenEngage()
        {
            if (TryTurnBackToAttackSlot(out bool slotAhead))
                return;

            // Место впереди: доезд с торможением, а не удар с места на полном ходу.
            if (slotAhead)
            {
                _chaseToAttackRange = TryStartAttackRangeRoute(_unitAggression.CurrentTarget);
                if (!_chaseToAttackRange)
                    EngageTarget();
                return;
            }

            if (!TryStartTurnBack(turnRight: _contactClockwise, onComplete: () => EngageTarget()))
                EngageTarget();
        }

        // Разворот сразу кончается в месте для атаки и тормозит там. Иначе он кончался бы спиной к цели,
        // и до места пришлось бы разворачиваться второй раз. Место впереди — разворот не нужен, туда ведёт обычный доезд.
        private bool TryTurnBackToAttackSlot(out bool slotAhead)
        {
            slotAhead = false;
            if (!_unitAggression.HasTarget || !TryGetForward(out FMVector2 forward))
                return false;

            UnitRuntime target = _unitAggression.CurrentTarget;
            FMVector2 slot = FindAttackSlot(target);
            float dx = slot.X - Position.X;
            float dy = slot.Y - Position.Y;
            if (forward.X * dx + forward.Y * dy > 0f)
            {
                slotAhead = true;
                return false;
            }

            if (!TryStartTurnBack(forward, TurnBackReach(2f), slot, 0.3f, stopAtEnd: true, onStart: null, onComplete: () => EngageTarget()))
                return false;

            ClaimAttackSlot(target, slot);
            return true;
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

        private bool RollOwnFall(UnitRuntime partner)
        {
            if (_fallReady && ReferenceEquals(_fallPartner, partner))
                return _fell;

            return StoreFall(partner);
        }

        // true, если этот юнит падает от контакта с partner. Чужой контакт и отсутствие броска оставляют его на ногах.
        // Ещё не разобранному юниту бросок считается здесь и сохраняется: его ResolveContact прочитает готовый исход.
        private bool PeekFall(UnitRuntime partner)
        {
            if (_fallReady && ReferenceEquals(_fallPartner, partner))
                return _fell;

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
