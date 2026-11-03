using ContractsServer.Command;
using ContractsServer.Core.Enum;
using Server.Shared.Core.Contracts;
using Server.Shared.Core.Contracts.WebSocket;
using Server.WebSocket.Core.Handler;
using Shared.Core.Contracts;
using Shared.Core.Contracts.Server;
using System;
using System.Threading.Tasks;

namespace Server.WebSocket.Service
{
    public partial class WebSocketService : IWebSocketService
    {
        #region Component
        private readonly IMatchService _matchService;
        private readonly HandlerWS _handlerWS;
        #endregion

        #region Times Fields
        // Resolved lazily: NetworkService depends on IWebSocketService, so a direct constructor dependency is circular.
        private readonly Func<INetworkService> _networkService;
        #endregion

        public WebSocketService(
            IMatchService matchService,
            Func<INetworkService> networkService)
        {
            _matchService = matchService;
            _networkService = networkService;

            _handlerWS = new(matchService, SendCommand);
        }

        public void ReceiveCommand(string command, object data)
        {
            switch (command)
            {
                case CommandWs.StartMatch: _handlerWS.StartMatch(data);
                    break;

                case CommandWs.SpawnUnit: _handlerWS.SpawnUnit(data);
                    break;

                default: break;
            }
        }

        public void SendCommand(string command, object data)
        {
            _networkService().Receive(command, data);
        }
    }
}
