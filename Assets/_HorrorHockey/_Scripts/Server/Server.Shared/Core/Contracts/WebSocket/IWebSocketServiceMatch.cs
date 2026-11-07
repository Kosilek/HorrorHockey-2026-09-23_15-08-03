using ContractsServer.Core.Enum;
using FM.Mathematics.DataTypes;

namespace Server.Shared.Core.Contracts.WebSocket
{
    public interface IWebSocketServiceMatch
    {
        void SpawnUnit(Byte2 typeUnit, Team team, PlayerIndex playerIndex);
    }
}
