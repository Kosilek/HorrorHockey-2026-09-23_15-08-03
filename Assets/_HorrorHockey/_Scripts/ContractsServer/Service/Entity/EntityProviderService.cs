using ContractsServer.Core.Enum;
using ContractsServer.Core.Interfaces;
using ContractsServer.Data;
using ContractsServer.Data.Entity;
using FM.Mathematics.DataTypes;
using Newtonsoft.Json;
using System.IO;
using UnityEngine;

namespace ContractsServer.Service.Entity
{
    public class EntityProviderService : IEntityProviderService
    {
        private string PathBaseRoute
        {
            get
            {
#if SERVER
return null;
#else
                return Path.Combine(
                    Application.dataPath,
                    "_HorrorHockey/_Scripts/ContractsServer/Json/Entity/Hockeyist.json");
#endif
            }
        }

        private EntityData _entityData;

        public EntityProviderService()
        {
            string json = File.ReadAllText(PathBaseRoute);
            _entityData = JsonConvert.DeserializeObject<EntityData>(json);
        }

        public EntityDataUnit GetDataUnit(Byte2 positionUnit)
        {
            if (!_entityData.TryGetDataUnit(positionUnit, out var data))
            {
                UnityEngine.Debug.LogError(
                    $"[EntityData] Не удалось получить данные юнита. Position: {positionUnit}");
            }
            return data;
        }

        public EntityDataUnit GetDataUnit(TypeEntityUnit type)
        {
            if (!_entityData.TryGetDataUnit(type, out var data))
            {
                UnityEngine.Debug.LogError(
                    $"[EntityData] Не удалось получить данные юнита. Type: {type}");
            }
            return data;
        }
    }
}