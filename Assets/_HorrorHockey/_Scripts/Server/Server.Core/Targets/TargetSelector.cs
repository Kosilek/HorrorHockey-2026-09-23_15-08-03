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

        public TargetSelector(
            IWorldGrid world,
            Func<int, UnitRuntime> funcGetUnit)
        {
            _worldGrid = world;
            _funcGetUnit = funcGetUnit;

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

                    currentTarget = _funcGetUnit(targets[0]);

                    if (currentTarget == null)
                    {
                        return false;
                    }
                    if (!ValidationRadiusDetection(unit.Position, currentTarget.Position, radiusDetection * radiusDetection))
                    {
                        return false;
                    }
                    return true;
                }

                int randSizeSpan = _random.Next(1, count / 2 + 1);
                float radiusDetectionSqr = radiusDetection * radiusDetection;
                int fillSpan = 0;
                Vector2 unitPosition = unit.Position;
                float minDist = float.MaxValue;
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

                    if (target == null || target.Id == unitId)
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

                    if (distanceSqr < minDist)
                    {
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

        private bool ValidationRadiusDetection(Vector2 unit, Vector2 target, float radiusDetectionSqr)
        {
            return radiusDetectionSqr > Vector2.DistanceSqr(unit, target);
        }
    }
}
