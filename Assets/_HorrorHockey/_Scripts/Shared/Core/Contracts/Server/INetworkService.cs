namespace Shared.Core.Contracts.Server
{
    public interface INetworkService
    {
        /// <summary>
        /// {RU} Принимает бинарный кадр от сервера. Разбор кадра и команды — внутри.
        /// <para>{EN} Accepts a binary frame from the server. Decoding stays inside.</para>
        /// </summary>
        void ReceiveFrame(byte[] frame);
        void Send(string command, object data);

        /// <summary>
        /// <para>{EN} Runs received commands. Call from the main thread.</para>
        /// {RU} Выполняет полученные команды. Вызывать из главного потока.
        /// </summary>
        void Dispatch();
    }
}