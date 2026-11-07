using ContractsServer.Core.Enum;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContractsServer.Data.Entity
{
    [Serializable]
    public class EntityDataCharacter
    {
        public CharacterData[] Characteristics { get; set; }
        private Dictionary<TypeCharater, float> _characters;
        public Dictionary<TypeCharater, float> Characters
        {
            get
            {
                if (_characters == null)
                {
                    _characters = Characteristics.ToDictionary(key => key.TypeCharater, value => value.BaseValue);
                }
                return _characters;
            }
        }

        public float ViewRadius => Characters[TypeCharater.ViewRadius];
        public float DetectionRadius => Characters[TypeCharater.DetectionRadius];

        public float AttackRadius => Characters[TypeCharater.AttackRadius];
        public float AttackSpeed => Characters[TypeCharater.AttackSpeed];
        public float Damage => Characters[TypeCharater.Damage];

        public float Health => Characters[TypeCharater.Health];
        public float Defense => Characters[TypeCharater.Defense];

        public float MovementSpeed => Characters[TypeCharater.MovementSpeed];

        public bool TryGetValue(TypeCharater type, out float value)
        {
            if (!Characters.TryGetValue(type, out value))
                return false;

            return true;
        }
    }
}
