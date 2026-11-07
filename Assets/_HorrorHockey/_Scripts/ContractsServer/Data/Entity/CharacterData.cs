using ContractsServer.Core.Enum;
using System;

namespace ContractsServer.Data.Entity
{
    [Serializable]
    public class CharacterData
    {
        public TypeCharater TypeCharater { get; set; }
        public float BaseValue { get; set; }
    }

    [Serializable]
    public class CharacterDataUpgrade
    {
        public TypeCharater TypeCharater { get; set; }
        public DataUpgrade[] Upgrade { get; set; }
    }

    [Serializable]
    public struct DataUpgrade
    {
        public float Value { get; set; }
        public int Cost { get; set; }
    }
}
