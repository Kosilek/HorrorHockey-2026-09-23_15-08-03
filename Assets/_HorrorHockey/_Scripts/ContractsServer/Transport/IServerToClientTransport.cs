namespace ContractsServer.Transport
{
    /// <summary>
    /// {RU} Доставка уже закодированного кадра на клиент. Замена реализации не меняет кодек и обработчики команд.
    /// <para>{EN} Delivers an encoded frame to the client. Replacing the implementation leaves the codec and command handlers as they are.</para>
    /// </summary>
    public interface IServerToClientTransport
    {
        void SendFrame(byte[] frame);
    }
}
