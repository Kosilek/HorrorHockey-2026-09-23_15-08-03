using ContractsServer.Command;
using ContractsServer.Core.Enum;
using ContractsServer.Data.Entity;
using FM.Mathematics.DataTypes;
using Server.Core.Data;
using Server.Core.Entity;
using Server.Core.Interfaces;
using Server.Shared.Configurations;

namespace Server.Core.Match
{
    public partial class MatchController
    {
        public void SpawnUnit(Byte2 typeUnit, Team team, PlayerIndex playerIndex)
        {
            _commands.Enqueue(() => Spawn(typeUnit, team, playerIndex));
        }

        private void Spawn(Byte2 typeUnit, Team team, PlayerIndex playerIndex)
        {
            IObjectSlot<UnitRuntime> slot = _objectPoolingService.GetFreeSlot();
            ushort id = (ushort)slot.Id;

            UnitFieldsPlayer fieldsPlayer = new()
            {
                Id = id,
                Team = team,
                PlayerIndex = playerIndex
            };

            EntityDataUnit entityData = _entityProviderService.GetDataUnit(typeUnit);

            UnitFieldsUnit fieldsUnit = new()
            {
                IndexUnit = typeUnit,

                MaxSpeed = 
                entityData.EntityDataCharacter.MovementSpeed
                + entityData.UpgradeMatch.GetCurrentValueUpgrade(TypeCharater.MovementSpeed, -1)
                + entityData.UpgradeProfile.GetCurrentValueUpgrade(TypeCharater.MovementSpeed, -1),

                AttackRadius =
                entityData.EntityDataCharacter.AttackRadius
                + entityData.UpgradeMatch.GetCurrentValueUpgrade(TypeCharater.AttackRadius, -1)
                + entityData.UpgradeProfile.GetCurrentValueUpgrade(TypeCharater.AttackRadius, -1),

                DetectionRadius =
                entityData.EntityDataCharacter.DetectionRadius
                + entityData.UpgradeMatch.GetCurrentValueUpgrade(TypeCharater.DetectionRadius, -1)
                + entityData.UpgradeProfile.GetCurrentValueUpgrade(TypeCharater.DetectionRadius, -1),

                AccelTime = entityData.EntityDataMain.AccelTime,
                BrakeTime = entityData.EntityDataMain.BrakeTime,
                FuncUpdateCell = _worldGrid.UpdateUnitCell
            };
            TeamSide side = playerIndex >= PlayerIndex.RedLeft ? TeamSide.Red : TeamSide.Blue;
            _navigationService.TryGetRoute(side, LaneOf(playerIndex), out Vector2[] route);

            UnitFieldsRoute fieldsRoute = new()
            {
                StartPosition = _navigationService.GetStartPosition(playerIndex),
                BaseRoute = route
            };

            UnitFieldsSpecial fieldsSpecial = new()
            {
                SendCommand = _webSocket.SendCommand,
                TargetSelector = _targetSelector
            };

            UnitRuntime unit = slot.TObj;
            unit.SetObject((fieldsPlayer, fieldsUnit, fieldsRoute, fieldsSpecial));
            slot.SetObject(unit);

            _units.Add(id, unit);
            unit.CurrentCell = _worldGrid.SetUnitCell(fieldsRoute.StartPosition, id);
            _webSocket.SendCommand(CommandWs.SpawnUnit, (fieldsUnit.IndexUnit, id, team, playerIndex));
        }

        /// <summary>
        /// <para>{EN} Sends the unit from its current position to the target around obstacles. Start and controls go to the client as is.</para>
        /// {RU} Ведёт юнита от текущей позиции к цели в обход препятствий. Start и controls уходят клиенту как есть.
        /// </summary>
        public void MoveUnitTo(int id, Vector2 target)
        {
            _commands.Enqueue(() => MoveTo(id, target));
        }

        private void MoveTo(int id, Vector2 target)
        {
            if (!_units.TryGetValue(id, out UnitRuntime unit))
                return;

            if (!_navigationService.TryBuildRoute(unit.Position, target, out Vector2[] route))
                return;
            if (!unit.TryStartRoute(route))
                return;

            UnityEngine.Debug.Log($"Create Route: {unit.Id}, {unit.RouteStart}, {unit.RouteControls}");
            _webSocket.SendCommand(CommandWs.CreateRoute, (unit.Id, unit.RouteStart, unit.RouteControls));
        }

        private static RouteLane LaneOf(PlayerIndex playerIndex)
        {
            return playerIndex switch
            {
                PlayerIndex.BlueMiddle or PlayerIndex.RedMiddle => RouteLane.Middle,
                PlayerIndex.BlueRight or PlayerIndex.RedRight => RouteLane.Right,
                _ => RouteLane.Left
            };
        }
    }
}
