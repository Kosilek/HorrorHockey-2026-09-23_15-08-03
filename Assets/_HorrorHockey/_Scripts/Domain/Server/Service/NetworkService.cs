using ContractsServer.BinaryProtocol;
using ContractsServer.Command;
using ContractsServer.Core.Enum;
using ContractsServer.Transport;
using Domain.Server.HTTP.Core.Interfaces;
using Domain.Server.HTTP.Service;
using Domain.Server.WebSocket.Core.Interfaces;
using Domain.Server.WebSocket.Service;
using FM.Mathematics.DataTypes;
using Shared.Core.Contracts;
using Shared.Core.Contracts.Server;
using System;
using System.Collections.Concurrent;
using UnityEngine;
using FmVector2 = FM.Mathematics.DataTypes.Vector2;

namespace Domain.Server.Service
{
    public class NetworkService : INetworkService
    {
        private readonly INetworkWSService _networkWS;
        private readonly INetworkHTTPService _networkHTTP;

        private readonly IClientToServerTransport _toServer;
        private readonly IRuntimeService _runtime;
        
        public NetworkService(
            IRuntimeService runtime,
            IClientToServerTransport toServer)
        {
            _runtime = runtime;
            _toServer = toServer;

            _networkWS = new NetworkWSService();
            _networkHTTP = new NetworkHTTPService();
        }

        // The frame arrives from the match thread. Unity objects are touched later, in Dispatch.
        private readonly ConcurrentQueue<(string command, object data)> _received = new();

        public void ReceiveFrame(byte[] frame)
        {
            if (!WsBinaryCodec.TryDecode(frame, out string command, out object data))
            {
                Debug.LogError("Binary decode failed on the client.");
                return;
            }

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
                case CommandWs.SlowUnit:
                    if (data is ValueTuple<ushort, float> slowData)
                        _runtime.SlowUnit(slowData.Item1, slowData.Item2);
                    break;
                case CommandWs.StunUnit:
                    if (data is ushort stunId)
                        _runtime.StunUnit(stunId);
                    break;
                case CommandWs.UnstunUnit:
                    if (data is ushort unstunId)
                        _runtime.UnstunUnit(unstunId);
                    break;
                case CommandWs.StopUnit:
                    if (data is ushort stopId)
                        _runtime.StopUnit(stopId);
                    break;
                case CommandWs.StartAnimationClip:
                    if (data is ValueTuple<int, byte> animationData)
                        _runtime.StartAnimationClip(animationData.Item1, animationData.Item2);
                    break;
                default: break;
            };
        }

        public void Send(string command, object data)
        {
            if (!WsBinaryCodec.TryEncode(command, data, out byte[] frame))
            {
                Debug.LogError($"Binary encode failed on the client: {command}");
                return;
            }

            _toServer.SendFrame(frame);
        }
    }
}
