namespace Server.Shared.Core.Contracts.WebSocket
{
    public interface IWebSocketService : IWebSocketServiceUnit, IWebSocketServiceMatch
    {
        /// <summary>
        /// {RU} Принимает бинарный кадр от клиента. Разбор кадра и команды — внутри.
        /// <para>{EN} Accepts a binary frame from the client. Decoding stays inside.</para>
        /// </summary>
        void ReceiveFrame(byte[] frame);
        void SendCommand(string command, object data);
    }
}
