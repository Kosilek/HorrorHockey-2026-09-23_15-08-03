using ContractsServer.Core.Enum;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContractsServer.Data.Entity
{
    [Serializable]
    public class EntityDataCharacterUpgrade
    {
        public CharacterDataUpgrade[] CharacterDataUpgrades { get; set; }
        private Dictionary<TypeCharater, DataUpgrade[]> _upgrades;
        private Dictionary<TypeCharater, DataUpgrade[]> Upgrades
        {
            get
            {
                if (_upgrades == null)
                {
                    _upgrades = CharacterDataUpgrades.ToDictionary(key => key.TypeCharater, value => value.Upgrade);
                }
                return _upgrades;
            }
        }


        public bool TryGetNextValue(TypeCharater type, int level, int money, out float nextValue, out int cost)
        {
            nextValue = 0;
            cost = 0;
            int index = level + 1;
            if (!ValidationUpgrade(type, index, money, out var dataUpgrades))
                return false;

            nextValue = dataUpgrades[index].Value;
            cost = dataUpgrades[index].Cost;

            return true;
        }

        public float GetCurrentValueUpgrade(TypeCharater type, int level)
        {
            float currentValue = 0f;
            if (level < 0)
            {
                return currentValue;
            }

            if (!Upgrades.TryGetValue(type, out DataUpgrade[] data))
            {
                UnityEngine.Debug.LogError(
                    $"[EntityUpgrade] Для характеристики '{type}' не предусмотрены улучшения.");
                return currentValue;
            }

            int maxIndex = Math.Min(level + 1, data.Length);
            for (int i = 0; i < maxIndex; i++)
            {
                currentValue += data[i].Value;
            }
            return currentValue;
        }

        private bool ValidationUpgrade(TypeCharater type, int index, int money, out DataUpgrade[] dataUpgrades)
        {
            if (!Upgrades.TryGetValue(type, out dataUpgrades))
                return false;

            if (index >= dataUpgrades.Length)
                return false;

            if (money < dataUpgrades[index].Cost)
                return false;

            return true;
        }
    }
}
