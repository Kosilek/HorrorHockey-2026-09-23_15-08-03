using ContractsServer.BinaryProtocol;
using ContractsServer.Command;
using ContractsServer.Transport;
using Server.Shared.Core.Contracts;
using Server.Shared.Core.Contracts.WebSocket;
using Server.WebSocket.Core.Handler;
using UnityEngine;

namespace Server.WebSocket.Service
{
    public partial class WebSocketService : IWebSocketService
    {
        #region Component
        private readonly IMatchService _matchService;
        private readonly HandlerWS _handlerWS;
        #endregion

        private readonly IServerToClientTransport _toClient;

        public WebSocketService(
            IMatchService matchService,
            IServerToClientTransport toClient)
        {
            _matchService = matchService;
            _toClient = toClient;

            _handlerWS = new(matchService, SendCommand);
        }

        public void ReceiveFrame(byte[] frame)
        {
            if (!WsBinaryCodec.TryDecode(frame, out string command, out object data))
            {
                Debug.LogError("Binary decode failed on the server.");
                return;
            }

            switch (command)
            {
                case CommandWs.StartMatch:
                    _handlerWS.StartMatch(data);
                    break;

                case CommandWs.SpawnUnit:
                    _handlerWS.SpawnUnit(data);
                    break;

                default: break;
            }
        }

        public void SendCommand(string command, object data)
        {
            if (!WsBinaryCodec.TryEncode(command, data, out byte[] frame))
            {
                Debug.LogError($"Binary encode failed on the server: {command}");
                return;
            }

            _toClient.SendFrame(frame);
        }
    }
}
