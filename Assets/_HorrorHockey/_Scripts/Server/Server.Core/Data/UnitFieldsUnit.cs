using FM.Mathematics.DataTypes;
using System;

namespace Server.Core.Data
{
    public class UnitFieldsUnit
    {
        public Byte2 IndexUnit { get; set; }
        public float MaxSpeed { get; set; }
        public float AccelTime { get; set; }
        public float BrakeTime { get; set; }
        public Func<Vector2, int, Int2, Int2> FuncUpdateCell { get; set; }

        public float DetectionRadius { get; set; }
        public float AttackRadius { get; set; }

        public float BodyWidth { get; set; }
        public float Mass { get; set; }

        public float StunTime { get; set; }
    }
}
