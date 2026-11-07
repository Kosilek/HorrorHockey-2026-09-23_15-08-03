using System;

namespace ContractsServer.Data
{
    [Serializable]
    public class UnlockRequirement
    {
        public int RequiredLevel { get; set; }
        public int Cost { get; set; }
    }
}
