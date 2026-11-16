using Codice.CM.Client.Differences.Graphic;
using ContractsServer.Core.Enum;
using ContractsServer.Data.Entity;
using Server.Core.Data;
using Server.Core.Module.Entity;
using System;

namespace Server.Core.Entity
{
    public partial class UnitRuntime
    {
        public void SetObject<TData>(TData data)
        {
            if (data is ValueTuple<UnitFieldsPlayer, UnitFieldsUnit, UnitFieldsRoute, UnitFieldsSpecial> dataUnit)
            {
                Id = dataUnit.Item1.Id;
                Team = dataUnit.Item1.Team;
                PlayerIndex = dataUnit.Item1.PlayerIndex;

                IndexUnit = dataUnit.Item2.EntityDataMain.Position;
                _funcUpdateCell = dataUnit.Item2.FuncUpdateCell;

                _baseRoute = dataUnit.Item3.BaseRoute;
                _followBaseRoute = true;
                _rejoinBaseRoute = false;
                RouteControls = null;

                var MaxSpeed =
                    dataUnit.Item2.EntityDataCharacter.MovementSpeed
                    + dataUnit.Item2.UpgradeMatch.GetCurrentValueUpgrade(TypeCharater.MovementSpeed, -1)
                    + dataUnit.Item2.UpgradeProfile.GetCurrentValueUpgrade(TypeCharater.MovementSpeed, -1);

                var AttackRadius =
                    dataUnit.Item2.EntityDataCharacter.AttackRadius
                    + dataUnit.Item2.UpgradeMatch.GetCurrentValueUpgrade(TypeCharater.AttackRadius, -1)
                    + dataUnit.Item2.UpgradeProfile.GetCurrentValueUpgrade(TypeCharater.AttackRadius, -1);

                var DetectionRadius =
                    dataUnit.Item2.EntityDataCharacter.DetectionRadius
                    + dataUnit.Item2.UpgradeMatch.GetCurrentValueUpgrade(TypeCharater.DetectionRadius, -1)
                    + dataUnit.Item2.UpgradeProfile.GetCurrentValueUpgrade(TypeCharater.DetectionRadius, -1);

                _routeMotion.Initialize(
                    MaxSpeed,
                    dataUnit.Item2.EntityDataMain.AccelTime,
                    dataUnit.Item2.EntityDataMain.BrakeTime,
                    dataUnit.Item3.StartPosition);

                BodyWidth = dataUnit.Item2.EntityDataMain.BodyWidth;
                Mass = dataUnit.Item2.EntityDataMain.Mass;

                StunTime = dataUnit.Item2.EntityDataMain.StunTime;

                UnitAggressionData unitAggressionData = new()
                {
                    Id = this.Id,
                    DetectionRadius = DetectionRadius,
                    AttackRadius = AttackRadius,
                    BodyWidth = BodyWidth
                };

                Health =
                    dataUnit.Item2.EntityDataCharacter.Health
                    + dataUnit.Item2.UpgradeMatch.GetCurrentValueUpgrade(TypeCharater.Health, -1)
                    + dataUnit.Item2.UpgradeProfile.GetCurrentValueUpgrade(TypeCharater.Health, -1);
                _maxHealth = Health;

                Defense =
                    dataUnit.Item2.EntityDataCharacter.Defense
                    + dataUnit.Item2.UpgradeMatch.GetCurrentValueUpgrade(TypeCharater.Defense, -1)
                    + dataUnit.Item2.UpgradeProfile.GetCurrentValueUpgrade(TypeCharater.Defense, -1);

                Damage =
                    dataUnit.Item2.EntityDataCharacter.Damage
                    + dataUnit.Item2.UpgradeMatch.GetCurrentValueUpgrade(TypeCharater.Damage, -1)
                    + dataUnit.Item2.UpgradeProfile.GetCurrentValueUpgrade(TypeCharater.Damage, -1);

                EntityDataAttackClips = dataUnit.Item2.EntityDataMain.ClipAttacks;

                _unitAggression = new(dataUnit.Item4.TargetSelector, unitAggressionData);
                _unitAggression.StatusChanged += OnStatusChanged;
                _unitAggression.TargetChanged += OnTargetChanged;
                _sendCommand = dataUnit.Item4.SendCommand;
                _navigationService = dataUnit.Item4.NavigationService;
                _getUnitsByTarget = dataUnit.Item4.GetUnitsByTarget;
                _fillNearbyUnits = dataUnit.Item4.FillNearbyUnits;
                _targetChanged = dataUnit.Item4.TargetChanged;
                _callDeath = dataUnit.Item4.CallDeath;
                _attackSlotTarget = null;
                _chargeTarget = null;
                _meetWith = null;
                _circleControls = null;
                _orbitControls = null;
                _circleComplete = null;
                _stunCount = 0;
                _stunTimer = 0f;
                _attackCharge = false;
                _chaseToAttackRange = false;
                _oldTargetDead = false;
                _contactWith = null;
                _contactPartner = null;
                _contactKeepsOrders = false;
                _fallen = false;
                _ignoreContacts = false;
                _cooldownContacts = 0f;
            }
            else if (data is UnitRuntime unit && ReferenceEquals(unit, this))
            {
                // Пул помечает слот занятым повторным SetObject уже заполненного юнита.
            }
            else
            {
                /// TODO: Log error
            }
        }

        public void ResetObject()
        {
        }

        public void UpdateData()
        {
        }
    }
}