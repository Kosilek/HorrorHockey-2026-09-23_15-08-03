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
                // Маршрут с торможением тоже входит с серверной скоростью, иначе клиент перед ним встанет и разгонится с нуля.
                if (stopAtEnd)
                    entityMovement.StartMovement(start, controls, startSpeed);
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

        public void SetTarget(ushort indexUnit, int indexTarget)
        {
            if (!_entityMovements.TryGetValue(indexUnit, out IEntityMovemet entityMovement))
                return;

            // Цель ещё не заспавнена на клиенте или её нет — юнит просто перестаёт к ней доворачиваться.
            IEntityMovemet target = null;
            if ((uint)indexTarget <= ushort.MaxValue)
                _entityMovements.TryGetValue((ushort)indexTarget, out target);

            entityMovement.SetTarget(target);
        }
    }
}
