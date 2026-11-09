using ContractsServer.Core.Enum;
using FM.Mathematics.DataTypes;
using System;
using FmVector2 = FM.Mathematics.DataTypes.Vector2;

namespace ContractsServer.BinaryProtocol.Codec
{
    /// <summary>
    /// {RU} Полезная нагрузка команд матча. Точки маршрута без квантования: клиент получает ту же ломаную, что посчитал сервер.
    /// <para>{EN} Match payloads. Route points stay unquantized so the client rebuilds the server polyline.</para>
    /// </summary>
    public static class MatchBinaryCodec
    {
        public const byte RouteStops = 0;
        public const byte RouteKeepsSpeed = 1;

        public static void WriteSpawnRequest(BinaryWriter writer, Byte2 slot, Team team, PlayerIndex player)
        {
            writer.WriteByte(slot.X);
            writer.WriteByte(slot.Y);
            writer.WriteByte(checked((byte)team));
            writer.WriteByte(checked((byte)player));
        }

        public static ValueTuple<Byte2, Team, PlayerIndex> ReadSpawnRequest(BinaryReader reader)
        {
            var slot = new Byte2(reader.ReadByte(), reader.ReadByte());
            var team = (Team)reader.ReadByte();
            var player = (PlayerIndex)reader.ReadByte();
            return (slot, team, player);
        }

        public static void WriteSpawned(BinaryWriter writer, Byte2 slot, ushort unitId, Team team, PlayerIndex player)
        {
            writer.WriteByte(slot.X);
            writer.WriteByte(slot.Y);
            writer.WriteUInt16(unitId);
            writer.WriteByte(checked((byte)team));
            writer.WriteByte(checked((byte)player));
        }

        public static ValueTuple<Byte2, ushort, Team, PlayerIndex> ReadSpawned(BinaryReader reader)
        {
            var slot = new Byte2(reader.ReadByte(), reader.ReadByte());
            ushort unitId = reader.ReadUInt16();
            var team = (Team)reader.ReadByte();
            var player = (PlayerIndex)reader.ReadByte();
            return (slot, unitId, team, player);
        }

        public static void WriteRoute(BinaryWriter writer, ushort unitId, FmVector2 start, FmVector2[] points)
        {
            WriteRouteHead(writer, unitId, start, points, RouteStops);
        }

        public static void WriteMovingRoute(
            BinaryWriter writer, ushort unitId, FmVector2 start, FmVector2[] points, bool stopAtEnd, float speed)
        {
            WriteRouteHead(writer, unitId, start, points, RouteKeepsSpeed);
            writer.WriteBool(stopAtEnd);
            writer.WriteFloat(speed);
        }

        public static object ReadRoute(BinaryReader reader)
        {
            ushort unitId = reader.ReadUInt16();
            var start = new FmVector2(reader.ReadFloat(), reader.ReadFloat());
            byte kind = reader.ReadByte();
            FmVector2[] points = ReadPoints(reader);

            if (kind == RouteStops)
                return (unitId, start, points);

            if (kind == RouteKeepsSpeed)
                return (unitId, start, points, reader.ReadBool(), reader.ReadFloat());

            throw new InvalidOperationException($"Unknown route kind: {kind}");
        }

        public static void WriteSlow(BinaryWriter writer, ushort unitId, float speed)
        {
            writer.WriteUInt16(unitId);
            writer.WriteFloat(speed);
        }

        public static ValueTuple<ushort, float> ReadSlow(BinaryReader reader) =>
            (reader.ReadUInt16(), reader.ReadFloat());

        public static void WriteUnit(BinaryWriter writer, ushort unitId) =>
            writer.WriteUInt16(unitId);

        public static ushort ReadUnit(BinaryReader reader) => reader.ReadUInt16();

        public static void WriteAnimation(BinaryWriter writer, int unitId, byte clip)
        {
            writer.WriteInt32(unitId);
            writer.WriteByte(clip);
        }

        public static ValueTuple<int, byte> ReadAnimation(BinaryReader reader) =>
            (reader.ReadInt32(), reader.ReadByte());

        private static void WriteRouteHead(BinaryWriter writer, ushort unitId, FmVector2 start, FmVector2[] points, byte kind)
        {
            points ??= Array.Empty<FmVector2>();
            BinaryValidation.ValidationOverflowUInt16(points.Length, nameof(WriteRouteHead));

            writer.WriteUInt16(unitId);
            writer.WriteFloat(start.X);
            writer.WriteFloat(start.Y);
            writer.WriteByte(kind);
            writer.WriteUInt16((ushort)points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                writer.WriteFloat(points[i].X);
                writer.WriteFloat(points[i].Y);
            }
        }

        private static FmVector2[] ReadPoints(BinaryReader reader)
        {
            int count = reader.ReadUInt16();
            var points = new FmVector2[count];
            for (int i = 0; i < count; i++)
                points[i] = new FmVector2(reader.ReadFloat(), reader.ReadFloat());
            return points;
        }
    }
}
