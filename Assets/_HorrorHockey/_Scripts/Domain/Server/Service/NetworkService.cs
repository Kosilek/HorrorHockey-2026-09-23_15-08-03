using ContractsServer.Command;
using ContractsServer.Core.Enum;
using Domain.Server.HTTP.Core.Interfaces;
using Domain.Server.HTTP.Service;
using Domain.Server.WebSocket.Core.Interfaces;
using Domain.Server.WebSocket.Service;
using FM.Mathematics.DataTypes;
using Server.Shared.Core.Contracts.WebSocket;
using Shared.Core.Contracts;
using Shared.Core.Contracts.Server;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using FmVector2 = FM.Mathematics.DataTypes.Vector2;

namespace Domain.Server.Service
{
    public class NetworkService : INetworkService
    {
        private readonly INetworkWSService _networkWS;
        private readonly INetworkHTTPService _networkHTTP;

        private readonly IWebSocketService _webSocket;
        private readonly IRuntimeService _runtime;
        
        public NetworkService(
            IRuntimeService runtime,
            IWebSocketService webSocket)
        {
            _runtime = runtime;
            _webSocket = webSocket;

            _networkWS = new NetworkWSService();
            _networkHTTP = new NetworkHTTPService();
        }

        // Receive is called from the match thread, Unity objects can only be touched from the main one.
        private readonly ConcurrentQueue<(string command, object data)> _received = new();

        public void Receive(string command, object data)
        {
            _received.Enqueue((command, data));
        }

        public void Dispatch()
        {
            while (_received.TryDequeue(out var received))
                Handle(received.command, received.data);
        }

        private void Handle(string command, object data)
        {
            switch (command)
            {
                case CommandWs.StartMatch:
                    
                    break;
                case CommandWs.SpawnUnit:
                    if (data is not null && data is ValueTuple<Byte2, ushort, Team, PlayerIndex> unitData)
                    {
                        _runtime.SpawnUnit(unitData.Item1, unitData.Item2, unitData.Item3, unitData.Item4);
                    }
                    break;
                case CommandWs.CreateRoute:
                    if (data is ValueTuple<ushort, FmVector2, FmVector2[], bool, float> routeMoving)
                    {
                        _runtime.CreateRoute(routeMoving.Item1, routeMoving.Item2, routeMoving.Item3, routeMoving.Item4, routeMoving.Item5);
                    }
                    else if (data is ValueTuple<ushort, FmVector2, FmVector2[]> routeData)
                    {
                        _runtime.CreateRoute(routeData.Item1, routeData.Item2, routeData.Item3);
                    }
                    break;
                default: break;
            };
        }

        public void Send(string command, object data)
        {
            switch (command)
            {
                case CommandWs.SpawnUnit:
                    _webSocket.ReceiveCommand(CommandWs.SpawnUnit, data);
                    break;
            }
        }
    }
}
