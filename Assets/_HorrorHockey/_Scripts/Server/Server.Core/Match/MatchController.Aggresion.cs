using Server.Core.Entity;
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

        private void OnUnitTargetChanged(UnitRuntime unit, UnitRuntime previous, UnitRuntime current)
        {
            if (previous != null)
                RemoveUnitAggresive(previous.Id, unit);
            if (current != null)
                AddUnitAggresive(current.Id, unit);
        }
    }
}
