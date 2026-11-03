namespace Shared.Core.Contracts.Server
{
    public interface INetworkService
    {
        void Receive(string command, object data);
        void Send(string command, object data);

        /// <summary>
        /// <para>{EN} Runs received commands. Call from the main thread.</para>
        /// {RU} Выполняет полученные команды. Вызывать из главного потока.
        /// </summary>
        void Dispatch();
    }
}