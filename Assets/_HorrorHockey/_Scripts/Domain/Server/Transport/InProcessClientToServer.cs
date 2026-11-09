using ContractsServer.Transport;
using Server.Shared.Core.Contracts.WebSocket;
using System;

namespace Domain.Server.Transport
{
    /// <summary>
    /// {RU} Клиент и сервер в одном процессе: кадр сразу приходит в <see cref="IWebSocketService.ReceiveFrame"/>.
    /// Для удалённого сервера заменяется классом, который отправляет <c>frame</c> в сокет.
    /// <para>{EN} Same process: the frame is handed to the server ingress. Swap this class for a socket sender later.</para>
    /// </summary>
    public sealed class InProcessClientToServer : IClientToServerTransport
    {
        private readonly Func<IWebSocketService> _server;

        public InProcessClientToServer(Func<IWebSocketService> server)
        {
            _server = server;
        }

        public void SendFrame(byte[] frame)
        {
            if (frame == null || frame.Length == 0)
                return;

            _server().ReceiveFrame(frame);
        }
    }
}
