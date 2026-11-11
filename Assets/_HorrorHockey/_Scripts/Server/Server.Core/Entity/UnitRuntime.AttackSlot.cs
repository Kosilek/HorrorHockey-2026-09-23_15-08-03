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

        /// <summary>
        /// <para>{EN} Where this unit stands, or is driving to, to attack the target. False if it holds no place near the target.</para>
        /// {RU} Где этот юнит стоит или куда едет, чтобы бить цель. false, если он не держит места у цели.
        /// </summary>
        public bool TryGetAttackSpot(UnitRuntime target, out FMVector2 spot)
        {
            if (ReferenceEquals(_attackSlotTarget, target))
            {
                spot = _attackSlot;
                return true;
            }

            // Без слота стоящий юнит занимает своё место, а едущий ещё неизвестно где встанет.
            spot = Position;
            return !IsBodyMoving;
        }

        private void ClaimAttackSlot(UnitRuntime target, FMVector2 slot)
        {
            _attackSlotTarget = target;
            _attackSlot = slot;
        }

        private void ClearAttackSlot()
        {
            _attackSlotTarget = null;
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
        /// of the other attackers. Rings go from the body width outwards, on a ring the place closest to this unit wins.
        /// Without a free place the unit squeezes in on the near ring where the neighbours leave the most room.</para>
        /// {RU} Место, откуда бить цель: на кольце вокруг неё внутри радиуса атаки, не касаясь цели и мест других атакующих.
        /// Кольца идут от ширины тел наружу, на кольце побеждает место, ближайшее к этому юниту.
        /// Без свободного места юнит втискивается на ближнее кольцо туда, где соседи оставляют больше всего места.
        /// </summary>
        private FMVector2 FindAttackSlot(UnitRuntime target)
        {
            FMVector2 center = target.Position;
            List<UnitRuntime> attackers = _getUnitsByTarget?.Invoke(target.Id);
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

                    float room = SlotRoom(target, attackers, point);
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

        // true, если на этом месте юнит не заходит ни в цель, ни в места других атакующих. Касание краями — ещё свободно.
        private bool IsFreeAttackSpot(UnitRuntime target, FMVector2 point)
        {
            float dx = point.X - target.Position.X;
            float dy = point.Y - target.Position.Y;
            float bodyGap = (BodyWidth + target.BodyWidth) * 0.5f;
            if (dx * dx + dy * dy < (bodyGap - _slotTolerance) * (bodyGap - _slotTolerance))
                return false;

            return SlotRoom(target, _getUnitsByTarget?.Invoke(target.Id), point) >= -_slotGap - _slotTolerance;
        }

        // Самый узкий зазор между краями тел до мест других атакующих. Меньше нуля — тела заходят друг в друга.
        private float SlotRoom(UnitRuntime target, List<UnitRuntime> attackers, FMVector2 point)
        {
            float room = float.MaxValue;
            if (attackers == null)
                return room;

            for (int i = 0; i < attackers.Count; i++)
            {
                UnitRuntime other = attackers[i];
                if (ReferenceEquals(other, this) || ReferenceEquals(other, target) || other.IsDead)
                    continue;
                if (!other.TryGetAttackSpot(target, out FMVector2 spot))
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
