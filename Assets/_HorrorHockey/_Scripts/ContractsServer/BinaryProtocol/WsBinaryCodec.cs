using ContractsServer.BinaryProtocol.Codec;
using ContractsServer.Command;
using ContractsServer.Core.Enum;
using FM.Mathematics.DataTypes;
using System;
using FmVector2 = FM.Mathematics.DataTypes.Vector2;

namespace ContractsServer.BinaryProtocol
{
    /// <summary>
    /// {RU} Кодирует и декодирует кадр целиком. Обе стороны используют один класс, поэтому вынос сервера не меняет формат.
    /// <para>{EN} Encodes and decodes a whole frame. Both sides share this class, so moving the server does not change the format.</para>
    /// </summary>
    public static class WsBinaryCodec
    {
        public static bool TryEncode(string command, object data, out byte[] frame)
        {
            frame = Array.Empty<byte>();
            if (string.IsNullOrEmpty(command))
                return false;

            try
            {
                var writer = new BinaryWriter();
                if (!TryWrite(writer, command, data))
                    return false;

                frame = writer.WrittenSpan.ToArray();
                return frame.Length >= MatchEnvelope.HeaderSize;
            }
            catch (Exception)
            {
                frame = Array.Empty<byte>();
                return false;
            }
        }

        public static bool TryDecode(ReadOnlyMemory<byte> frame, out string command, out object data)
        {
            command = null;
            data = null;

            if (frame.Length < MatchEnvelope.HeaderSize)
                return false;

            try
            {
                var reader = new BinaryReader();
                reader.Initialize(frame);
                BinaryMessageType messageType = MatchEnvelope.ReadHeader(reader);
                if (!WsBinaryCommandMap.TryGetCommand(messageType, out command))
                    return false;

                data = ReadPayload(reader, messageType);
                return true;
            }
            catch (Exception)
            {
                command = null;
                data = null;
                return false;
            }
        }

        private static bool TryWrite(BinaryWriter writer, string command, object data)
        {
            switch (command)
            {
                case CommandWs.StartMatch:
                    MatchEnvelope.WriteHeader(writer, BinaryMessageType.StartMatch);
                    return true;

                case CommandWs.SpawnUnit when data is ValueTuple<Byte2, Team, PlayerIndex> request:
                    MatchEnvelope.WriteHeader(writer, BinaryMessageType.SpawnUnitRequest);
                    MatchBinaryCodec.WriteSpawnRequest(writer, request.Item1, request.Item2, request.Item3);
                    return true;

                case CommandWs.SpawnUnit when data is ValueTuple<Byte2, ushort, Team, PlayerIndex> spawned:
                    MatchEnvelope.WriteHeader(writer, BinaryMessageType.SpawnUnit);
                    MatchBinaryCodec.WriteSpawned(writer, spawned.Item1, spawned.Item2, spawned.Item3, spawned.Item4);
                    return true;

                case CommandWs.CreateRoute when data is ValueTuple<ushort, FmVector2, FmVector2[]> route:
                    MatchEnvelope.WriteHeader(writer, BinaryMessageType.CreateRoute);
                    MatchBinaryCodec.WriteRoute(writer, route.Item1, route.Item2, route.Item3);
                    return true;

                case CommandWs.CreateRoute when data is ValueTuple<ushort, FmVector2, FmVector2[], bool, float> moving:
                    MatchEnvelope.WriteHeader(writer, BinaryMessageType.CreateRoute);
                    MatchBinaryCodec.WriteMovingRoute(writer, moving.Item1, moving.Item2, moving.Item3, moving.Item4, moving.Item5);
                    return true;

                case CommandWs.SlowUnit when data is ValueTuple<ushort, float> slow:
                    MatchEnvelope.WriteHeader(writer, BinaryMessageType.SlowUnit);
                    MatchBinaryCodec.WriteSlow(writer, slow.Item1, slow.Item2);
                    return true;

                case CommandWs.StunUnit when data is ushort stunId:
                    MatchEnvelope.WriteHeader(writer, BinaryMessageType.StunUnit);
                    MatchBinaryCodec.WriteUnit(writer, stunId);
                    return true;

                case CommandWs.UnstunUnit when data is ushort unstunId:
                    MatchEnvelope.WriteHeader(writer, BinaryMessageType.UnstunUnit);
                    MatchBinaryCodec.WriteUnit(writer, unstunId);
                    return true;

                case CommandWs.StopUnit when data is ushort stopId:
                    MatchEnvelope.WriteHeader(writer, BinaryMessageType.StopUnit);
                    MatchBinaryCodec.WriteUnit(writer, stopId);
                    return true;

                case CommandWs.StartAnimationClip when data is ValueTuple<int, byte> clip:
                    MatchEnvelope.WriteHeader(writer, BinaryMessageType.StartAnimationClip);
                    MatchBinaryCodec.WriteAnimation(writer, clip.Item1, clip.Item2);
                    return true;

                default:
                    return false;
            }
        }

        private static object ReadPayload(BinaryReader reader, BinaryMessageType messageType)
        {
            switch (messageType)
            {
                case BinaryMessageType.StartMatch:
                    return null;
                case BinaryMessageType.SpawnUnitRequest:
                    return MatchBinaryCodec.ReadSpawnRequest(reader);
                case BinaryMessageType.SpawnUnit:
                    return MatchBinaryCodec.ReadSpawned(reader);
                case BinaryMessageType.CreateRoute:
                    return MatchBinaryCodec.ReadRoute(reader);
                case BinaryMessageType.SlowUnit:
                    return MatchBinaryCodec.ReadSlow(reader);
                case BinaryMessageType.StunUnit:
                case BinaryMessageType.UnstunUnit:
                case BinaryMessageType.StopUnit:
                    return MatchBinaryCodec.ReadUnit(reader);
                case BinaryMessageType.StartAnimationClip:
                    return MatchBinaryCodec.ReadAnimation(reader);
                default:
                    throw new ArgumentException($"Unsupported message type: {messageType}");
            }
        }
    }
}
