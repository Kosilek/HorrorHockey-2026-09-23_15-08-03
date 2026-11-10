using ContractsServer.Data.Entity;
using FM.Mathematics.DataTypes;
using System;

namespace Server.Core.Data
{
    public class UnitFieldsUnit
    {
        public EntityDataMain EntityDataMain { get; set; }
        public EntityDataCharacter EntityDataCharacter { get; set; }
        public EntityDataCharacterUpgrade UpgradeProfile { get; set; }
        public EntityDataCharacterUpgrade UpgradeMatch { get; set; }
        public Func<Vector2, int, Int2, Int2> FuncUpdateCell { get; set; }
    }
}
