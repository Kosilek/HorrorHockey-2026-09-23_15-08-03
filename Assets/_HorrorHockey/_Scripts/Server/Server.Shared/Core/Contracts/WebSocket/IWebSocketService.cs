namespace Server.Shared.Core.Contracts.WebSocket
{
    public interface IWebSocketService : IWebSocketServiceUnit, IWebSocketServiceMatch
    {
        void ReceiveCommand(string command, object data);
        void SendCommand(string command, object data);
    }
}
