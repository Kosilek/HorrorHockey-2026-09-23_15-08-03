using ContractsServer.Core.Enum;
using ContractsServer.Utils.Expansion;
using FM.Mathematics.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContractsServer.Data.Entity
{
    [Serializable]
    public class EntityData
    {
        public EntityDataUnit[] EntityDataUnits { get; set; }

        private Dictionary<TypeEntityUnit, EntityDataUnit> _unitDatas;
        private Dictionary<TypeEntityUnit, EntityDataUnit> UnitDatas
        {
            get
            {
                _unitDatas ??= EntityDataUnits.ToDictionary(key => key.TypeEntityUnit);

                return _unitDatas;
            }
        }

        public bool TryGetDataUnit(TypeEntityUnit type, out EntityDataUnit data)
        {
            if (!UnitDatas.TryGetValue(type, out data))
                return false;

            return true;    
        }

        public bool TryGetDataUnit(Byte2 positionUnit, out EntityDataUnit data)
        {
            TypeEntityUnit type = positionUnit.GetPositionUnit();
            return TryGetDataUnit(type, out data);
        }
    }

    [Serializable]
    public class EntityDataUnit
    {
        public TypeEntityUnit TypeEntityUnit { get; set; }
        public EntityDataMain EntityDataMain { get; set; }
        public EntityDataCharacter EntityDataCharacter { get; set; }
        public EntityDataCharacterUpgrade UpgradeProfile { get; set; }
        public EntityDataCharacterUpgrade UpgradeMatch { get; set; }
    }
}
