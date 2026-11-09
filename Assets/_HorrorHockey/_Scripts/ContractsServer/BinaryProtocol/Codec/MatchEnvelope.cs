using System;

namespace ContractsServer.BinaryProtocol.Codec
{
    public static class MatchEnvelope
    {
        public const int HeaderSize = 2;

        public static void WriteHeader(BinaryWriter writer, BinaryMessageType messageType)
        {
            BinaryValidation.ValidationNullable(writer, nameof(WriteHeader));
            writer.WriteByte(BinaryConfiguration.Version);
            writer.WriteByte(messageType.Encode());
        }

        public static BinaryMessageType ReadHeader(BinaryReader reader)
        {
            BinaryValidation.ValidationNullable(reader, nameof(ReadHeader));
            byte version = reader.ReadByte();
            if (version != BinaryConfiguration.Version)
            {
                throw new InvalidOperationException($"Unsupported binary protocol version: {version}");
            }

            return BinaryMessageTypeExtensions.Decode(reader.ReadByte());
        }
    }
}
