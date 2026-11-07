using ContractsServer.Command;
using ContractsServer.Core.Enum;
using FM.Mathematics.DataTypes;
using System;

namespace Server.WebSocket.Core.Handler
{
    internal partial class HandlerWS
    {
        internal bool StartMatch(object data) // Test method to start a match, can be called from the WebSocket service
        {
            _matchService.AddMatch();
            return true;
        }

        internal void SpawnUnit(object data)
        {
            if (data is ValueTuple<Byte2, Team, PlayerIndex> spawnData)
            {
                _matchService.SpawnUnit(spawnData.Item1, spawnData.Item2, spawnData.Item3);
            }
        }
    }
}
