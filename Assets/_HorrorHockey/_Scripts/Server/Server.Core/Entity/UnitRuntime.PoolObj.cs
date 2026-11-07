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

                IndexUnit = dataUnit.Item2.IndexUnit;
                _funcUpdateCell = dataUnit.Item2.FuncUpdateCell;

                _baseRoute = dataUnit.Item3.BaseRoute;
                _followBaseRoute = true;
                RouteControls = null;
                _routeMotion.Initialize(
                    dataUnit.Item2.MaxSpeed,
                    dataUnit.Item2.AccelTime,
                    dataUnit.Item2.BrakeTime,
                    dataUnit.Item3.StartPosition);

                UnitAggressionData unitAggressionData = new()
                {
                    Id = this.Id,
                    DetectionRadius = dataUnit.Item2.DetectionRadius,
                    AttackRadius = dataUnit.Item2.AttackRadius
                };
                _unitAggression = new(dataUnit.Item4.TargetSelector, unitAggressionData);
                _sendCommand = dataUnit.Item4.SendCommand;
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