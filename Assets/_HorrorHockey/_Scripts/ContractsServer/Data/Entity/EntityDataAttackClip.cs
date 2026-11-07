using ContractsServer.Core.Enum;
using System;

namespace ContractsServer.Data.Entity
{
    [Serializable]
    public class EntityDataAttackClip
    {
        public int Id { get; set; }
        public bool IsRange { get; set; }
        public TypeBullet TypeBullet { get; set; }
        public float Duration { get; set; }
        public float TickDamage { get; set; }
        public int Weight { get; set; }
    }
}
