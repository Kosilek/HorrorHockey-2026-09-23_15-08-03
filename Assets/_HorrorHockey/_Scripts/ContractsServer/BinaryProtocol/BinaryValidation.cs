using System;

namespace ContractsServer.BinaryProtocol
{
    public static class BinaryValidation
    {
        public static void ValidationVersion(byte version, string actionName)
        {
            if (version != BinaryConfiguration.Version)
            {
                throw new ArgumentOutOfRangeException(nameof(version),
                    $"Version {version} is not supported in {actionName}");
            }
        }

        public static void ValidationMessageType(
            byte messageType, BinaryMessageType expectedType, string actionName)
        {
            if (messageType != expectedType.Encode())
            {
                throw new ArgumentOutOfRangeException(nameof(messageType),
                    $"Message type {messageType} is not supported in {actionName}");
            }
        }

        public static void ValidationNegativeValue(int value, string actionName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"Value {value} is negative in {actionName}");
            }
        }

        public static void ValidationOverflowInt32(long value, string actionName)
        {
            if (value > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"Value {value} is overflow in {actionName}");
            }
        }

        public static void ValidationOverflowUInt16(int value, string actionName)
        {
            if (value > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"Value {value} is overflow in {actionName}");
            }
        }

        public static void ValidationOverflowByte(int value, string actionName)
        {
            if (value > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"Value {value} is overflow in {actionName}");
            }
        }

        public static void ValidationNullable<T>(T value, string actionName) where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value), $"Value is null in {actionName}");
            }
        }
    }
}
