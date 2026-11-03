using ContractsServer.Core.Enum;
using FM.Mathematics.DataTypes;
using Shared.Core.Contracts;
using System.Collections.Generic;
using FmVector2 = FM.Mathematics.DataTypes.Vector2;

namespace Domain.Game.Services
{
    public partial class RuntimeService
    {
        public void SpawnUnit(Byte2 unitSlot, ushort indexUnit, Team team, PlayerIndex playerIndex)
        {
            var unitInstObj = _providerService.ProviderUnitService.GetUnit(unitSlot);
            var unit = _objectLifeCicleService.IntantiateObject(unitInstObj);
            _entityMovements.TryAdd(indexUnit, unit as IEntityMovemet);
        }

        public void CreateRoute(ushort indexUnit, FmVector2 start, FmVector2[] controls)
        {
            if (_entityMovements.TryGetValue(indexUnit, out IEntityMovemet entityMovement))
            {
                entityMovement.StartMovement(start, controls);
            }
        }
    }
}
