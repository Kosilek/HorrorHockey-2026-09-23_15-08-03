namespace _FM.DI
{
    /// <summary>
    /// {RU} Интерфейс для освобождения ресурсов (аналог System.IDisposable для использования в DI контейнере)
    /// <para>{EN} Interface for resource cleanup (analog of System.IDisposable for use in DI container)</para>
    /// </summary>
    public interface IDisposable
    {
        /// <summary>
        /// {RU} Освобождает занимаемые ресурсы
        /// <para>{EN} Releases occupied resources</para>
        /// </summary>
        void Dispose();
    }
}