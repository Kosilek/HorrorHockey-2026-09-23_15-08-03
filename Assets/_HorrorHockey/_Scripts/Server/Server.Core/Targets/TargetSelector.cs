using FM.Mathematics.DataTypes;
using Server.Core.Entity;
using Server.Core.Interfaces;
using System;
using System.Buffers;

namespace Server.Core.Targets
{
    public class TargetSelector
    {
        private readonly IWorldGrid _worldGrid;
        private readonly Random _random;
        private Func<int, UnitRuntime> _funcGetUnit;
        private Func<int, int> _funcAttackerCount;

        // Сколько врагов в выборке смотреть как минимум. Иначе при малом их числе случайная выборка часто видит одного,
        // и все выбирают того, кого уже бьют, вместо свободного.
        private const int _minSampleHostiles = 8;

        public TargetSelector(
            IWorldGrid world,
            Func<int, UnitRuntime> funcGetUnit,
            Func<int, int> funcAttackerCount)
        {
            _worldGrid = world;
            _funcGetUnit = funcGetUnit;
            _funcAttackerCount = funcAttackerCount;

            _random = new();
        }

        public bool TrySelectorTargetUnit(Int2 cell, int unitId, float radiusDetection, out UnitRuntime currentTarget, out float distance)
        {
            currentTarget = null;
            distance = default;
            UnitRuntime unit = _funcGetUnit(unitId);
            if (unit == null)
            {
                return false;
            }
            (int[] targets, int count) = _worldGrid.GetNeighborUnitIds(cell);
     
            try
            {
                if (count < 1)
                {
                    return false;
                }
                if (count == 1)
                {
                    if (targets[0] == unitId)
                    {
                        return false;
                    }

                    UnitRuntime single = _funcGetUnit(targets[0]);

                    if (!IsHostile(unit, single))
                    {
                        return false;
                    }
                    if (!ValidationRadiusDetection(unit.Position, single.Position, radiusDetection * radiusDetection))
                    {
                        return false;
                    }
                    currentTarget = single;
                    distance = Vector2.DistanceSqr(unit.Position, single.Position);
                    return true;
                }

                int randSizeSpan = Math.Max(_random.Next(1, count / 2 + 1), Math.Min(count, _minSampleHostiles));
                float radiusDetectionSqr = radiusDetection * radiusDetection;
                int fillSpan = 0;
                Vector2 unitPosition = unit.Position;
                float minDist = float.MaxValue;
                int minAttackers = int.MaxValue;
                int id = -1;

                int index = _random.Next(0, count);

                for (int i = 0; fillSpan < randSizeSpan && i < count; i++)
                {
                    if (index >= count)
                    {
                        index = 0;
                    }

                    UnitRuntime target = _funcGetUnit(targets[index]);
                    index++;

                    if (target == null || target.Id == unitId || !IsHostile(unit, target))
                    {
                        continue;
                    }

                    float distanceSqr = Vector2.DistanceSqr(
                        unitPosition,
                        target.Position);

                    if (distanceSqr >= radiusDetectionSqr)
                    {
                        continue;
                    }

                    // Сначала тот, кого бьёт меньше всего юнитов, среди равных — ближайший: пары расходятся по разным врагам, а не толпятся на одном.
                    int attackers = _funcAttackerCount?.Invoke(target.Id) ?? 0;
                    if (attackers < minAttackers || attackers == minAttackers && distanceSqr < minDist)
                    {
                        minAttackers = attackers;
                        minDist = distanceSqr;
                        id = target.Id;
                        currentTarget = target;
                        distance = minDist;
                    }
                    fillSpan++;
                }

                return id >= 0;
            }
            finally
            {
                ArrayPool<int>.Shared.Return(targets);
            }
        }

        /// <summary>
        /// <para>{EN} A live unit of the other team. Allies are never targets.</para>
        /// {RU} Живой юнит другой команды. Союзники никогда не становятся целью.
        /// </summary>
        public static bool IsHostile(UnitRuntime unit, UnitRuntime other)
        {
            return other != null && !other.IsDead && other.Team != unit.Team;
        }

        private bool ValidationRadiusDetection(Vector2 unit, Vector2 target, float radiusDetectionSqr)
        {
            return radiusDetectionSqr > Vector2.DistanceSqr(unit, target);
        }
    }
}
