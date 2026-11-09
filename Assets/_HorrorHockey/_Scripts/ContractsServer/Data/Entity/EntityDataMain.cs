using ContractsServer.Core.Enum;
using FM.Mathematics.DataTypes;
using System;

namespace ContractsServer.Data.Entity
{
    [Serializable]
    public class EntityDataMain
    {
        public EntityDataAttackClip[] ClipAttacks;
        public float AnimationSpeed { get; set; }
        public Byte2 Position { get; set; }
        public RewardData[] KillRewards { get; set; }
        public float BodyWidth { get; set; }
        public TypeEntity EntityType { get; set; }

        public bool IsSystemLocked { get; set; }
        public bool IsHidden { get; set; }
        public bool IsSelectable { get; set; }

        public float AccelTime { get; set; }
        public float BrakeTime { get; set; }

        public float Mass { get; set; }

        public float StunTime { get; set; }

        public UnlockRequirement UnlockRequirements { get; set; }
    }
}
