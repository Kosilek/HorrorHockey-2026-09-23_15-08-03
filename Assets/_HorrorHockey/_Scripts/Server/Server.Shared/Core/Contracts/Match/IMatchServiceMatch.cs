using ContractsServer.Core.Enum;
using FM.Mathematics.DataTypes;
using System;

namespace Server.Shared.Core.Contracts
{
    public interface IMatchServiceMatch
    {
        void SpawnUnit(Byte2 typeUnit, Team team, PlayerIndex playerIndex);
    }
}
