using System;
using System.Text;

namespace ContractsServer.BinaryProtocol
{
    public class BinaryReader
    {
        private ReadOnlyMemory<byte> _buffer;
        private int _position;
        public int Remaining => _buffer.Length - _position;

        private void EnsureAvailable(int bytes)
        {
            BinaryValidation.ValidationNegativeValue(bytes, nameof(EnsureAvailable));
            if (_position + bytes > _buffer.Length)
                throw new InvalidOperationException("Not enough data in buffer.");
        }

        public void Initialize(ReadOnlyMemory<byte> buffer)
        {
            _buffer = buffer;
            _position = 0;
        }

        public byte ReadByte()
        {
            EnsureAvailable(1);
            byte value = _buffer.Span[_position];
            _position++;
            return value;
        }

        public ushort ReadUInt16()
        {
            EnsureAvailable(2);
            ushort value = (ushort)(_buffer.Span[_position] | (_buffer.Span[_position + 1] << 8));
            _position += 2;
            return value;
        }

        public short ReadInt16()
        {
            EnsureAvailable(2);
            short value = (short)(_buffer.Span[_position] | (_buffer.Span[_position + 1] << 8));
            _position += 2;
            return value;
        }

        public int ReadInt32()
        {
            EnsureAvailable(4);
            int value = _buffer.Span[_position]
                | (_buffer.Span[_position + 1] << 8)
                | (_buffer.Span[_position + 2] << 16)
                | (_buffer.Span[_position + 3] << 24);
            _position += 4;
            return value;
        }

        public long ReadInt64()
        {
            EnsureAvailable(8);
            uint low = (uint)(_buffer.Span[_position]
                | (_buffer.Span[_position + 1] << 8)
                | (_buffer.Span[_position + 2] << 16)
                | (_buffer.Span[_position + 3] << 24));
            uint high = (uint)(_buffer.Span[_position + 4]
                | (_buffer.Span[_position + 5] << 8)
                | (_buffer.Span[_position + 6] << 16)
                | (_buffer.Span[_position + 7] << 24));
            _position += 8;
            return (long)(((ulong)high << 32) | low);
        }

        public string ReadUtf8String()
        {
            ushort length = ReadUInt16();
            if (length == 0)
            {
                return string.Empty;
            }

            EnsureAvailable(length);
            string text = Encoding.UTF8.GetString(_buffer.Span.Slice(_position, length));
            _position += length;
            return text;
        }

        public float ReadFloat()
        {
            EnsureAvailable(4);
            float value = BitConverter.ToSingle(_buffer.Span.Slice(_position, 4));
            _position += 4;
            return value;
        }

        public bool ReadBool() => ReadByte() != 0;

        public ReadOnlyMemory<byte> ReadRawBytes(int length)
        {
            if (length == 0)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            EnsureAvailable(length);
            ReadOnlyMemory<byte> slice = _buffer.Slice(_position, length);
            _position += length;
            return slice;
        }
    }
}
