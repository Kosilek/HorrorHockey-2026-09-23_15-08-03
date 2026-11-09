using ContractsServer.Transport;
using Shared.Core.Contracts.Server;
using System;

namespace Server.WebSocket.Transport
{
    /// <summary>
    /// {RU} Сервер и клиент в одном процессе: кадр сразу приходит в <see cref="INetworkService.ReceiveFrame"/>.
    /// Для удалённой машины заменяется рассылкой этих же байт по сокету.
    /// <para>{EN} Same process: the frame is handed to the client ingress. Swap this class for a socket broadcast later.</para>
    /// </summary>
    public sealed class InProcessServerToClient : IServerToClientTransport
    {
        private readonly Func<INetworkService> _client;

        public InProcessServerToClient(Func<INetworkService> client)
        {
            _client = client;
        }

        public void SendFrame(byte[] frame)
        {
            if (frame == null || frame.Length == 0)
                return;

            _client().ReceiveFrame(frame);
        }
    }
}
