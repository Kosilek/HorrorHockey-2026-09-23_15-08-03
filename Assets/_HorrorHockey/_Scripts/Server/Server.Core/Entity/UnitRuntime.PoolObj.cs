using Server.Shared.Data;
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

                indexUnit = dataUnit.Item2.indexUnit;

                _baseRoute = dataUnit.Item3.BaseRoute;
                _followBaseRoute = true;
                RouteControls = null;
                _routeMotion.Initialize(
                    dataUnit.Item2.maxSpeed,
                    dataUnit.Item2.accelTime,
                    dataUnit.Item2.brakeTime,
                    dataUnit.Item3.StartPosition);

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