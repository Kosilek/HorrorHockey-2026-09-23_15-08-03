using System;

namespace ContractsServer.BinaryProtocol
{
    /// <summary>
    /// {RU} Тип binary-фрейма. Заголовок кадра: <c>[version u8][type u8][payload]</c>. Little-endian.
    /// <para>{EN} Binary frame type. Header is <c>[version u8][type u8][payload]</c>, little-endian.</para>
    /// </summary>
    public enum BinaryMessageType
    {
        /// <summary>{RU} Клиент → сервер. Пустой payload.<para>{EN} Client to server. Empty payload.</para></summary>
        StartMatch = 1,

        /// <summary>
        /// {RU} Клиент → сервер. <c>x u8, y u8, team u8, player u8</c>.
        /// <para>{EN} Client to server. Slot, team and player.</para>
        /// </summary>
        SpawnUnitRequest = 2,

        /// <summary>
        /// {RU} Сервер → клиент. <c>x u8, y u8, unitId u16, team u8, player u8</c>.
        /// <para>{EN} Server to client. Slot, unit id, team and player.</para>
        /// </summary>
        SpawnUnit = 3,

        /// <summary>
        /// {RU} Сервер → клиент. <c>unitId u16, start f32×2, kind u8, count u16, (x f32, y f32)×count</c>.
        /// <c>kind 0</c> — маршрут до остановки. <c>kind 1</c> — ещё <c>stopAtEnd u8, speed f32</c>.
        /// <para>{EN} Server to client. Route polyline. Kind 1 appends stopAtEnd and speed.</para>
        /// </summary>
        CreateRoute = 4,

        /// <summary>{RU} Сервер → клиент. <c>unitId u16, speed f32</c>.<para>{EN} Server to client.</para></summary>
        SlowUnit = 5,

        /// <summary>{RU} Сервер → клиент. <c>unitId u16</c>.<para>{EN} Server to client.</para></summary>
        StunUnit = 6,

        /// <summary>{RU} Сервер → клиент. <c>unitId u16</c>.<para>{EN} Server to client.</para></summary>
        UnstunUnit = 7,

        /// <summary>{RU} Сервер → клиент. <c>unitId u16</c>.<para>{EN} Server to client.</para></summary>
        StopUnit = 8,

        /// <summary>{RU} Сервер → клиент. <c>unitId i32, clip u8</c>.<para>{EN} Server to client.</para></summary>
        StartAnimationClip = 9
    }

    public static class BinaryMessageTypeExtensions
    {
        public static byte Encode(this BinaryMessageType type) => type switch
        {
            BinaryMessageType.StartMatch => 1,
            BinaryMessageType.SpawnUnitRequest => 2,
            BinaryMessageType.SpawnUnit => 3,
            BinaryMessageType.CreateRoute => 4,
            BinaryMessageType.SlowUnit => 5,
            BinaryMessageType.StunUnit => 6,
            BinaryMessageType.UnstunUnit => 7,
            BinaryMessageType.StopUnit => 8,
            BinaryMessageType.StartAnimationClip => 9,
            _ => throw new ArgumentException($"Invalid binary message type: {type}")
        };

        public static BinaryMessageType Decode(byte value) => value switch
        {
            1 => BinaryMessageType.StartMatch,
            2 => BinaryMessageType.SpawnUnitRequest,
            3 => BinaryMessageType.SpawnUnit,
            4 => BinaryMessageType.CreateRoute,
            5 => BinaryMessageType.SlowUnit,
            6 => BinaryMessageType.StunUnit,
            7 => BinaryMessageType.UnstunUnit,
            8 => BinaryMessageType.StopUnit,
            9 => BinaryMessageType.StartAnimationClip,
            _ => throw new ArgumentException($"Invalid binary message type byte: {value}")
        };

        public static bool TryDecode(byte value, out BinaryMessageType type)
        {
            try
            {
                type = Decode(value);
                return true;
            }
            catch (ArgumentException)
            {
                type = default;
                return false;
            }
        }
    }
}
