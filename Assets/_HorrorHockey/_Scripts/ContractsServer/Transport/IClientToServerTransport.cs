namespace ContractsServer.Transport
{
    /// <summary>
    /// {RU} Доставка уже закодированного кадра на сервер. Сейчас кадр остаётся в процессе. На отдельной машине этот же метод пишет байты в сокет.
    /// <para>{EN} Delivers an encoded frame to the server. In-process today. On a remote machine the same method writes the bytes to a socket.</para>
    /// </summary>
    public interface IClientToServerTransport
    {
        void SendFrame(byte[] frame);
    }
}
