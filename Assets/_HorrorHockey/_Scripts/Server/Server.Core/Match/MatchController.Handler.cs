using Server.Core.Entity;
using System.Buffers;

namespace Server.Core.Match
{
    public partial class MatchController
    {
        private UnitRuntime GetUnit(int unitId)
        {
            if (_units.TryGetValue(unitId, out var unit))
            {
                return unit;
            }
            return null;
        }
    }
}
