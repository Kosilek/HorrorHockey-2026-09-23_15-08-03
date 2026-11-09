using System;
using System.Text;

namespace ContractsServer.BinaryProtocol
{
    public class BinaryWriter
    {
        private byte[] _buffer = new byte[256];
        private int _position;

        public int Position => _position;
        public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _position);

        public void EnsureCapacity(int additionalBytes)
        {
            BinaryValidation.ValidationNegativeValue(additionalBytes, nameof(EnsureCapacity));
            long required = (long)_position + additionalBytes;
            BinaryValidation.ValidationOverflowInt32(required, nameof(EnsureCapacity));
            if (required > _buffer.Length)
            {
                Array.Resize(ref _buffer, (int)Math.Max((long)_buffer.Length * 2, required));
            }
        }

        public void WriteByte(byte value)
        {
            EnsureCapacity(1);
            _buffer[_position++] = value;
        }

        public void WriteUInt16(ushort value)
        {
            EnsureCapacity(2);
            _buffer[_position++] = (byte)value;
            _buffer[_position++] = (byte)(value >> 8);
        }

        public void WriteInt16(short value)
        {
            WriteUInt16((ushort)value);
        }

        public void WriteInt32(int value)
        {
            EnsureCapacity(4);
            _buffer[_position++] = (byte)value;
            _buffer[_position++] = (byte)(value >> 8);
            _buffer[_position++] = (byte)(value >> 16);
            _buffer[_position++] = (byte)(value >> 24);
        }

        public void WriteInt64(long value)
        {
            EnsureCapacity(8);
            _buffer[_position++] = (byte)value;
            _buffer[_position++] = (byte)(value >> 8);
            _buffer[_position++] = (byte)(value >> 16);
            _buffer[_position++] = (byte)(value >> 24);
            _buffer[_position++] = (byte)(value >> 32);
            _buffer[_position++] = (byte)(value >> 40);
            _buffer[_position++] = (byte)(value >> 48);
            _buffer[_position++] = (byte)(value >> 56);
        }

        public void WriteUtf8String(string text)
        {
            text ??= string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            BinaryValidation.ValidationOverflowUInt16(bytes.Length, nameof(WriteUtf8String));
            WriteUInt16((ushort)bytes.Length);
            if (bytes.Length > 0)
            {
                EnsureCapacity(bytes.Length);
                bytes.CopyTo(_buffer, _position);
                _position += bytes.Length;
            }
        }

        public void WriteFloat(float value)
        {
            WriteRawBytes(BitConverter.GetBytes(value));
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteRawBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length == 0)
            {
                return;
            }

            EnsureCapacity(bytes.Length);
            bytes.CopyTo(_buffer.AsSpan(_position));
            _position += bytes.Length;
        }

        public void Reset()
        {
            Array.Clear(_buffer, 0, _position);
            _position = 0;
        }
    }
}
