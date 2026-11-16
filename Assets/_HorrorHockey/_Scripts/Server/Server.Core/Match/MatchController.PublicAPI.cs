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
                EntityDataMain = entityData.EntityDataMain,
                EntityDataCharacter = entityData.EntityDataCharacter,
                UpgradeProfile = entityData.UpgradeProfile,
                UpgradeMatch = entityData.UpgradeMatch,
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
                TargetSelector = _targetSelector,
                NavigationService = _navigationService,
                GetUnitsByTarget = GetAllUnitTarget,
                FillNearbyUnits = FillNearbyUnits,
                TargetChanged = OnUnitTargetChanged,
                CallDeath = ResetAllTarget
            };

            UnitRuntime unit = slot.TObj;
            unit.SetObject((fieldsPlayer, fieldsUnit, fieldsRoute, fieldsSpecial));
            slot.SetObject(unit);

            _units.Add(id, unit);
            unit.CurrentCell = _worldGrid.SetUnitCell(fieldsRoute.StartPosition, id);
            _webSocket.SendCommand(CommandWs.SpawnUnit, (fieldsUnit.EntityDataMain.Position, id, team, playerIndex));
        }

        /// <summary>
        /// <para>{EN} Tells the client to start an animation clip on the unit.</para>
        /// {RU} Сообщает клиенту, что у юнита нужно запустить клип анимации.
        /// </summary>
        public void StartAnimationClip(int indexUnit, byte animationClip)
        {
            _commands.Enqueue(() => SendAnimationClip(indexUnit, animationClip));
        }

        private void SendAnimationClip(int indexUnit, byte animationClip)
        {
            if (!_units.ContainsKey(indexUnit))
                return;

            _webSocket.SendCommand(CommandWs.StartAnimationClip, (indexUnit, animationClip));
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
