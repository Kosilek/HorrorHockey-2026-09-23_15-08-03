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
            CreateRoute(indexUnit, start, controls, stopAtEnd: true, startSpeed: 0f);
        }

        public void CreateRoute(ushort indexUnit, FmVector2 start, FmVector2[] controls, bool stopAtEnd, float startSpeed)
        {
            if (_entityMovements.TryGetValue(indexUnit, out IEntityMovemet entityMovement))
            {
                if (stopAtEnd)
                    entityMovement.StartMovement(start, controls);
                else
                    entityMovement.StartMovingRoute(start, controls, startSpeed);
            }
        }

        public void SlowUnit(ushort indexUnit, float speed)
        {
            if (_entityMovements.TryGetValue(indexUnit, out IEntityMovemet entityMovement))
                entityMovement.SlowTo(speed);
        }

        public void StunUnit(ushort indexUnit)
        {
            if (_entityMovements.TryGetValue(indexUnit, out IEntityMovemet entityMovement))
                entityMovement.PauseMovement();
        }

        public void UnstunUnit(ushort indexUnit)
        {
            if (_entityMovements.TryGetValue(indexUnit, out IEntityMovemet entityMovement))
                entityMovement.ResumeMovement();
        }

        public void StopUnit(ushort indexUnit)
        {
            if (_entityMovements.TryGetValue(indexUnit, out IEntityMovemet entityMovement))
                entityMovement.StopMovement();
        }

        public void StartAnimationClip(int indexUnit, byte animationClip)
        {
            if ((uint)indexUnit > ushort.MaxValue)
                return;

            if (_entityMovements.TryGetValue((ushort)indexUnit, out IEntityMovemet entityMovement))
                entityMovement.StartAnimationClip(animationClip);
        }
    }
}
