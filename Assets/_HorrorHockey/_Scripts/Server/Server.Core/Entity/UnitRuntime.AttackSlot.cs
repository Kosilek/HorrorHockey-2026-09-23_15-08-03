using ContractsServer.Command;
using Server.Core.Enums;
using System;
using System.Collections.Generic;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace Server.Core.Entity
{
    public partial class UnitRuntime
    {
        // Зазор между краями тел в слоте, чтобы соседи и цель не касались друг друга.
        private const float _slotGap = 0.05f;
        // Допуск, с которым текущее место считается свободным и слот не нужен.
        private const float _slotTolerance = 0.02f;
        private const int _slotMinSamples = 8;
        private const int _slotMaxSamples = 64;

        // Куда этот юнит встал или едет, чтобы бить цель. Соседи по цели обходят эту точку.
        private UnitRuntime _attackSlotTarget;
        private FMVector2 _attackSlot;

        // По кому бежит чардж. Пока юнит едет, его точка удара занята: следующие с той же полосы выбирают другую.
        private UnitRuntime _chargeTarget;

        // Встречный чардж: с кем договорена общая точка встречи и где этот юнит в ней стоит. Считает её тот из пары, кто первый, сразу на обоих.
        private UnitRuntime _meetWith;
        private FMVector2 _meetAim;

        // Насколько шагов вбок от линии сближения можно сдвинуть точку встречи с каждой стороны.
        private const int _meetMaxSideSteps = 4;

        // Юниты вокруг, переиспользуемый список. Заполняется перед каждым поиском места.
        private readonly List<UnitRuntime> _nearby = new();

        /// <summary>
        /// <para>{EN} Where this unit stands, or is going to stand, in a fight: its attack place, the hit point of a running charge,
        /// or its position when it stands. False for a unit just moving by its route: nobody knows yet where it stops.</para>
        /// {RU} Где этот юнит стоит или встанет в драке: его место для атаки, точка удара бегущего чарджа
        /// или своя позиция, если он стоит. false для юнита, который просто едет по маршруту: где он встанет, ещё неизвестно.
        /// </summary>
        public bool TryGetOccupiedSpot(out FMVector2 spot)
        {
            spot = Position;
            if (IsDead)
                return false;

            if (_attackSlotTarget != null)
            {
                spot = _attackSlot;
                return true;
            }

            if (_chargeTarget != null && IsBodyMoving)
            {
                spot = _chargePoint;
                return true;
            }

            return !IsBodyMoving;
        }

        private List<UnitRuntime> GetNearbyUnits(UnitRuntime around)
        {
            _nearby.Clear();
            _fillNearbyUnits?.Invoke(around.CurrentCell, _nearby);
            return _nearby;
        }

        private void ClaimAttackSlot(UnitRuntime target, FMVector2 slot)
        {
            _attackSlotTarget = target;
            _attackSlot = slot;
            _chargeTarget = null;
            _meetWith = null;
        }

        private void ClaimChargePoint(UnitRuntime target, FMVector2 point)
        {
            _attackSlotTarget = null;
            _chargeTarget = target;
            _chargePoint = point;
        }

        private void ClearAttackSlot()
        {
            _attackSlotTarget = null;
            _chargeTarget = null;
            _meetWith = null;
        }

        private void OnTargetChanged(UnitRuntime previous, UnitRuntime current)
        {
            ClearAttackSlot();
            NotifyTarget(previous, current);
        }

        // Мёртвый больше никого не бьёт и места у цели не держит.
        private void ReleaseTargetOnDeath(StatusUnit status)
        {
            if (status != StatusUnit.Die)
                return;

            ClearAttackSlot();
            if (_unitAggression.CurrentTarget != null)
                NotifyTarget(_unitAggression.CurrentTarget, null);
        }

        // Реестр атакующих на сервере и цель на клиенте: клиент по ней доворачивает корпус.
        private void NotifyTarget(UnitRuntime previous, UnitRuntime current)
        {
            _targetChanged?.Invoke(this, previous, current);
            _sendCommand?.Invoke(CommandWs.SetTarget, (Id, current != null ? (int)current.Id : -1));
        }

        // Радиус ближнего кольца — тела касаются краями с зазором. Дальнее кольцо не выходит из дальности атаки.
        private void GetSlotRings(UnitRuntime target, out float near, out float far)
        {
            near = (BodyWidth + target.BodyWidth) * 0.5f + _slotGap;
            far = MathF.Max(near, _unitAggression.GetAttackReach(target) - _attackRangeInset);
        }

        /// <summary>
        /// <para>{EN} Place to attack the target from: on a ring around it inside the attack radius, without touching the target or the places
        /// taken by the units around. Rings go from the body width outwards, on a ring the place closest to this unit wins.
        /// Without a free place the unit squeezes in on the near ring where the neighbours leave the most room.</para>
        /// {RU} Место, откуда бить цель: на кольце вокруг неё внутри радиуса атаки, не касаясь цели и мест, занятых юнитами рядом.
        /// Кольца идут от ширины тел наружу, на кольце побеждает место, ближайшее к этому юниту.
        /// Без свободного места юнит втискивается на ближнее кольцо туда, где соседи оставляют больше всего места.
        /// </summary>
        private FMVector2 FindAttackSlot(UnitRuntime target)
        {
            FMVector2 center = target.Position;
            List<UnitRuntime> nearby = GetNearbyUnits(target);
            GetSlotRings(target, out float near, out float far);

            float dx = Position.X - center.X;
            float dy = Position.Y - center.Y;
            float baseAngle = dx * dx + dy * dy > 1e-8f
                ? MathF.Atan2(dy, dx)
                : FallbackSlotAngle();

            float ringStep = MathF.Max(BodyWidth, 0.1f);
            bool hasDense = false;
            FMVector2 dense = default;
            float denseRoom = float.MinValue;

            for (float ring = near; ring <= far + 1e-4f; ring += ringStep)
            {
                bool isNear = ring == near;
                int samples = SlotSamples(ring);
                float angleStep = 2f * MathF.PI / samples;

                // Обход от ближайшего к юниту угла поочерёдно в обе стороны.
                for (int i = 0; i < samples; i++)
                {
                    int offset = (i + 1) / 2;
                    float angle = baseAngle + ((i & 1) == 0 ? offset : -offset) * angleStep;
                    var point = new FMVector2(center.X + MathF.Cos(angle) * ring, center.Y + MathF.Sin(angle) * ring);
                    if (_navigationService != null && !_navigationService.IsWalkable(point))
                        continue;

                    float room = SlotRoom(target, nearby, point);
                    if (room >= 0f)
                        return point;

                    if (isNear && room > denseRoom)
                    {
                        denseRoom = room;
                        dense = point;
                        hasDense = true;
                    }
                }
            }

            if (hasDense)
                return dense;

            return new FMVector2(center.X + MathF.Cos(baseAngle) * near, center.Y + MathF.Sin(baseAngle) * near);
        }

        /// <summary>
        /// <para>{EN} End of the charge route: a point on the sum of the radii from the target center, a bit inside so the touch counts.
        /// From the side of this unit, unless other units already stand or run there: then the closest free point on the same ring
        /// within a quarter turn, so the unit steers a little and hits the target, not the back of an ally.
        /// False when the near side is full: point is then the straight one.</para>
        /// {RU} Конец маршрута чарджа: точка на сумме радиусов от центра цели, чуть внутрь, чтобы касание засчиталось.
        /// Со стороны юнита, если там не стоят и не бегут другие юниты. Иначе ближайшая свободная точка на том же кольце
        /// в пределах четверти оборота: юнит немного доворачивает и врезается в цель, а не в спину союзника.
        /// false, если ближняя сторона занята: тогда point — прямая точка.
        /// </summary>
        private bool TryFindChargePoint(UnitRuntime target, out FMVector2 point)
        {
            float ring = (BodyWidth + target.BodyWidth) * 0.5f - _approachOverlap;
            FMVector2 straight = GetApproachPoint(target, ring);
            point = straight;
            List<UnitRuntime> nearby = GetNearbyUnits(target);
            if (SlotRoom(target, nearby, straight) >= 0f)
                return true;

            FMVector2 center = target.Position;
            float dx = Position.X - center.X;
            float dy = Position.Y - center.Y;
            if (dx * dx + dy * dy <= ring * ring)
                return true;

            float baseAngle = MathF.Atan2(dy, dx);
            int samples = SlotSamples(ring);
            float angleStep = 2f * MathF.PI / samples;

            // Поочерёдно в обе стороны от прямой к юниту, не дальше четверти оборота: дальше путь шёл бы через саму цель.
            for (int i = 1; i <= samples / 2; i++)
            {
                int offset = (i + 1) / 2;
                float angle = baseAngle + ((i & 1) == 0 ? offset : -offset) * angleStep;
                var candidate = new FMVector2(center.X + MathF.Cos(angle) * ring, center.Y + MathF.Sin(angle) * ring);
                if (_navigationService != null && !_navigationService.IsWalkable(candidate))
                    continue;

                if (SlotRoom(target, nearby, candidate) >= 0f)
                {
                    point = candidate;
                    return true;
                }
            }

            return false;
        }

        // Сдвиг меньше шага выборки по кольцу — та же точка удара, маршрут не перестраивается.
        private bool IsSameChargePoint(FMVector2 point)
        {
            float tolerance = MathF.Max(BodyWidth * 0.25f, 0.05f);
            float dx = point.X - _chargePoint.X;
            float dy = point.Y - _chargePoint.Y;
            return dx * dx + dy * dy <= tolerance * tolerance;
        }

        /// <summary>
        /// <para>{EN} Head-on charge: both run at each other. The meeting point is where the bodies meet on a straight line by their speeds.
        /// If another fight takes that place, the point moves sideways from the line until it is free. It is counted once per pair and written to both,
        /// so both drive to the same place and collide there. Counted again once somebody takes the place. False if this is not a head-on pair.</para>
        /// {RU} Встречный чардж: оба бегут друг на друга. Точка встречи — где сойдутся тела по прямой с учётом скоростей.
        /// Если там чужая драка, точка сдвигается вбок от линии сближения до свободного места. Считается один раз на пару и пишется обоим,
        /// поэтому оба едут в одно место и сталкиваются там. Пересчитывается, если место заняли. false, если это не встречная пара.
        /// </summary>
        /// <param name="aim">{EN} Where the center of this unit goes. {RU} Куда едет центр этого юнита.</param>
        private bool TryGetMeetAim(UnitRuntime target, out FMVector2 aim)
        {
            aim = default;
            if (!IsMeetPair(target))
                return false;

            List<UnitRuntime> nearby = GetNearbyUnits(this);
            if (ReferenceEquals(_meetWith, target) && ReferenceEquals(target._meetWith, this)
                && SlotRoom(target, nearby, _meetAim) >= 0f)
            {
                aim = _meetAim;
                return true;
            }

            PlanMeet(target, nearby, out aim, out FMVector2 targetAim);
            _meetWith = target;
            _meetAim = aim;
            target._meetWith = this;
            target._meetAim = targetAim;
            return true;
        }

        private bool IsMeetPair(UnitRuntime target)
        {
            return target.IsBodyMoving
                && ReferenceEquals(target.CurrentTarget, this)
                && !_chaseToAttackRange && !target._chaseToAttackRange
                && !IsInFight && !target.IsInFight;
        }

        // Без свободного места пара встречается на самой линии сближения.
        private void PlanMeet(UnitRuntime target, List<UnitRuntime> nearby, out FMVector2 aim, out FMVector2 targetAim)
        {
            FMVector2 from = Position;
            FMVector2 to = target.Position;
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            aim = from;
            targetAim = to;
            if (distance <= 1e-5f)
                return;

            float axisX = dx / distance;
            float axisY = dy / distance;
            float half = ((BodyWidth + target.BodyWidth) * 0.5f - _approachOverlap) * 0.5f;
            float speed = MeetSpeed();
            float targetSpeed = target.MeetSpeed();
            float share = speed + targetSpeed > 1e-5f ? speed / (speed + targetSpeed) : 0.5f;
            float along = MathF.Max(0f, distance - half * 2f) * share + half;
            float midX = from.X + axisX * along;
            float midY = from.Y + axisY * along;
            float step = MathF.Max(BodyWidth, target.BodyWidth) + _slotGap;

            aim = new FMVector2(midX - axisX * half, midY - axisY * half);
            targetAim = new FMVector2(midX + axisX * half, midY + axisY * half);

            // Сдвиг поочерёдно в обе стороны, поперёк линии сближения: курс обоих меняется чуть-чуть.
            for (int i = 0; i <= 2 * _meetMaxSideSteps; i++)
            {
                int offset = (i + 1) / 2;
                float side = ((i & 1) == 0 ? offset : -offset) * step;
                float centerX = midX - axisY * side;
                float centerY = midY + axisX * side;
                var self = new FMVector2(centerX - axisX * half, centerY - axisY * half);
                var other = new FMVector2(centerX + axisX * half, centerY + axisY * half);
                if (_navigationService != null && (!_navigationService.IsWalkable(self) || !_navigationService.IsWalkable(other)))
                    continue;
                if (SlotRoom(target, nearby, self) < 0f || target.SlotRoom(this, nearby, other) < 0f)
                    continue;

                aim = self;
                targetAim = other;
                return;
            }
        }

        // Не ниже половины максимальной: стоящий на старте всё равно разгонится, пока пара сближается.
        private float MeetSpeed()
        {
            return MathF.Max(_routeMotion.CurrentSpeed, _routeMotion.MaxSpeed * 0.5f);
        }

        // true, если на этом месте юнит не заходит ни в цель, ни в места других юнитов рядом. Касание краями — ещё свободно.
        private bool IsFreeAttackSpot(UnitRuntime target, FMVector2 point)
        {
            float dx = point.X - target.Position.X;
            float dy = point.Y - target.Position.Y;
            float bodyGap = (BodyWidth + target.BodyWidth) * 0.5f;
            if (dx * dx + dy * dy < (bodyGap - _slotTolerance) * (bodyGap - _slotTolerance))
                return false;

            return SlotRoom(target, GetNearbyUnits(target), point) >= -_slotGap - _slotTolerance;
        }

        // Самый узкий зазор между краями тел до мест, занятых юнитами рядом из обеих команд: соседние драки тоже обходятся.
        // Тот, кого не считаем (цель или пара по встрече), пропускается. Меньше нуля — тела заходят друг в друга.
        private float SlotRoom(UnitRuntime skip, List<UnitRuntime> nearby, FMVector2 point)
        {
            float room = float.MaxValue;
            for (int i = 0; i < nearby.Count; i++)
            {
                UnitRuntime other = nearby[i];
                if (ReferenceEquals(other, this) || ReferenceEquals(other, skip))
                    continue;
                if (!other.TryGetOccupiedSpot(out FMVector2 spot))
                    continue;

                float dx = point.X - spot.X;
                float dy = point.Y - spot.Y;
                float gap = MathF.Sqrt(dx * dx + dy * dy) - (BodyWidth + other.BodyWidth) * 0.5f - _slotGap;
                if (gap < room)
                    room = gap;
            }

            return room;
        }

        // Шаг по кольцу — четверть ширины тела, чтобы не перескочить узкую щель между соседями.
        private int SlotSamples(float ring)
        {
            float spacing = MathF.Max(BodyWidth * 0.25f, 0.05f);
            int samples = (int)MathF.Ceiling(2f * MathF.PI * ring / spacing);
            return Math.Clamp(samples, _slotMinSamples, _slotMaxSamples);
        }

        // Юнит стоит в центре цели: встаём позади своего курса.
        private float FallbackSlotAngle()
        {
            if (TryGetForward(out FMVector2 forward))
                return MathF.Atan2(-forward.Y, -forward.X);

            return 0f;
        }

        // Прямая к слоту может пройти сквозь цель, если слот на другой стороне. Тогда ведём через точку на дальнем кольце между углами.
        private FMVector2[] BuildSlotControls(UnitRuntime target, FMVector2 slot)
        {
            FMVector2 center = target.Position;
            FMVector2 from = Position;
            GetSlotRings(target, out float near, out float far);

            // Юнит, уже зашедший в цель, выходит из неё по прямой, если прямая не ныряет глубже.
            float fromX = from.X - center.X;
            float fromY = from.Y - center.Y;
            float limitSqr = MathF.Min(near * near, fromX * fromX + fromY * fromY) - 1e-4f;
            if (DistanceToSegmentSqr(center, from, slot) >= limitSqr)
                return new[] { slot };

            float fromAngle = MathF.Atan2(from.Y - center.Y, from.X - center.X);
            float slotAngle = MathF.Atan2(slot.Y - center.Y, slot.X - center.X);
            float delta = slotAngle - fromAngle;
            while (delta > MathF.PI) delta -= 2f * MathF.PI;
            while (delta < -MathF.PI) delta += 2f * MathF.PI;

            float midAngle = fromAngle + delta * 0.5f;
            float midRadius = MathF.Max(far, near + BodyWidth * 0.5f);
            var mid = new FMVector2(center.X + MathF.Cos(midAngle) * midRadius, center.Y + MathF.Sin(midAngle) * midRadius);
            if (_navigationService != null && !_navigationService.IsWalkable(mid))
                return new[] { slot };

            return new[] { mid, slot };
        }

        private static float DistanceToSegmentSqr(FMVector2 point, FMVector2 from, FMVector2 to)
        {
            float sx = to.X - from.X;
            float sy = to.Y - from.Y;
            float lengthSqr = sx * sx + sy * sy;
            float t = lengthSqr > 1e-8f
                ? Math.Clamp(((point.X - from.X) * sx + (point.Y - from.Y) * sy) / lengthSqr, 0f, 1f)
                : 0f;

            float dx = from.X + sx * t - point.X;
            float dy = from.Y + sy * t - point.Y;
            return dx * dx + dy * dy;
        }
    }
}
