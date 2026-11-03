using FM.Mathematics.DataTypes;
using Shared.Core.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shared.Data.Provider
{
    [CreateAssetMenu(fileName = "ProviderUnitService", menuName = "Provider/ProviderUnitService")]
    public class ProviderUnitService : ScriptableObject
    {
        [SerializeField] private ProviderUnitServiceData[] _providerUnitData;
        internal Dictionary<Byte2, IInstObj> ProviderUnitData;

        public void Initialize()
        {
            ProviderUnitData = new Dictionary<Byte2, IInstObj>();
            if (_providerUnitData == null)
                return;

            foreach (var unit in _providerUnitData)
            {
                if (unit.Unit is IInstObj instObj)
                {
                    ProviderUnitData.Add(unit.SlotUnit, instObj);
                }
                else
                {
                    Debug.LogError($"Unit {unit.Unit.name} does not implement IInstObj interface.");
                }
            }
        }

        public IInstObj GetUnit(Byte2 slotUnit)
        {
            if (ProviderUnitData.TryGetValue(slotUnit, out IInstObj unit))
            {
                return unit;
            }
            else
            {
                Debug.LogError($"No unit found for slot {slotUnit}.");
                return null;
            }
        }

        [Serializable]
        internal class ProviderUnitServiceData
        {
            public Byte2 SlotUnit;
            public MonoBehaviour Unit;
        }
    }
}