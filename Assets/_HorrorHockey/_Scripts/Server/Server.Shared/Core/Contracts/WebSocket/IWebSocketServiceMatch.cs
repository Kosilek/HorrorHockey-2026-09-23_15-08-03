using ContractsServer.Core.Enum;

namespace Server.Shared.Core.Contracts.WebSocket
{
    public interface IWebSocketServiceMatch
    {
        void SpawnUnit(Team team, PlayerIndex playerIndex);
    }
}
