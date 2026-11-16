using FM.Mathematics.DataTypes;
using Server.Core.Entity;
using System.Buffers;
using System.Collections.Generic;

namespace Server.Core.Match
{
    public partial class MatchController
    {
        public void AddUnitAggresive(int target, UnitRuntime unit)
        {
            if (!_unitAggresices.TryGetValue(target, out var units))
            {
                units = new List<UnitRuntime>();
                _unitAggresices.Add(target, units);
            }

            if (!units.Contains(unit))
                units.Add(unit);
        }

        public void RemoveUnitAggresive(int target, UnitRuntime unit)
        {
            if (_unitAggresices.TryGetValue(target, out var units))
                units.Remove(unit);
        }

        public List<UnitRuntime> GetAllUnitTarget(int target)
        {
            if (_unitAggresices.TryGetValue(target, out var units))
            {
                return units;
            }
            return null;
        }

        public void ResetAllTarget(int unitId)
        {
            List<UnitRuntime> units = GetAllUnitTarget(unitId);
            if (units == null || units.Count == 0)
                return;

            // ResetTarget снимает юнита с этой цели и вынимает его из списка прямо во время обхода.
            UnitRuntime[] snapshot = units.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
                snapshot[i]?.ResetTargetForKill();
        }

        private int GetAttackerCount(int target)
        {
            return _unitAggresices.TryGetValue(target, out var units) ? units.Count : 0;
        }

        private void FillNearbyUnits(Int2 cell, List<UnitRuntime> units)
        {
            units.Clear();
            (int[] ids, int count) = _worldGrid.GetNeighborUnitIds(cell);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    if (_units.TryGetValue(ids[i], out UnitRuntime unit))
                        units.Add(unit);
                }
            }
            finally
            {
                ArrayPool<int>.Shared.Return(ids);
            }
        }

        private void OnUnitTargetChanged(UnitRuntime unit, UnitRuntime previous, UnitRuntime current)
        {
            if (previous != null)
                RemoveUnitAggresive(previous.Id, unit);
            if (current != null)
                AddUnitAggresive(current.Id, unit);
        }
    }
}
