using ContractsServer.Core.Enum;
using FM.Mathematics.DataTypes;

namespace ContractsServer.Utils.Expansion
{
    public static class ExpansionTypeEntityUnit
    {
        public static Byte2 GetPositionUnit(this TypeEntityUnit type)
        {
            return type switch
            {
                TypeEntityUnit.Hockeyist => new Byte2(0, 0),
                _ => default
            };
        }

        public static TypeEntityUnit GetPositionUnit(this Byte2 type)
        {
            return type switch
            {
                (0, 0) => TypeEntityUnit.Hockeyist,
                _ => default
            };
        }
    }
}
