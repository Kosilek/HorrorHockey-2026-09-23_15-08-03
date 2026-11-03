using ContractsServer.Command;
using ContractsServer.Core.Enum;
using FM.Mathematics.DataTypes;
using Server.Core.Entity;
using Server.Core.Interfaces;
using Server.Shared.Configurations;
using Server.Shared.Data;

namespace Server.Core.Match
{
    public partial class MatchController
    {
        public void SpawnUnit(Team team, PlayerIndex playerIndex)
        {
            _commands.Enqueue(() => Spawn(team, playerIndex));
        }

        private void Spawn(Team team, PlayerIndex playerIndex)
        {
            IObjectSlot<UnitRuntime> slot = _objectPoolingService.GetFreeSlot();
            ushort id = (ushort)slot.Id;

            UnitFieldsPlayer fieldsPlayer = new()
            {
                Id = id,
                Team = team,
                PlayerIndex = playerIndex
            };
            UnitFieldsUnit fieldsUnit = new() // default values
            {
                indexUnit = new Byte2(0, 0),
                maxSpeed = 5f,
                accelTime = 1f,
                brakeTime = 1f
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
                SendCommand = _webSocket.SendCommand
            };

            UnitRuntime unit = slot.TObj;
            unit.SetObject((fieldsPlayer, fieldsUnit, fieldsRoute, fieldsSpecial));
            slot.SetObject(unit);

            _units.Add(id, unit);
            _webSocket.SendCommand(CommandWs.SpawnUnit, (fieldsUnit.indexUnit, id, team, playerIndex));
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
