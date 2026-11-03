using ContractsServer.Core.Enum;
using System;

namespace Server.Shared.Core.Contracts
{
    public interface IMatchServiceMatch
    {
        void SpawnUnit(Team team, PlayerIndex playerIndex);
    }
}
