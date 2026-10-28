namespace _FM.DI
{
    /// <summary>
    /// {RU} Интерфейс фабрики для создания экземпляров объектов (используется внутри контейнера для Transient сервисов)
    /// <para>{EN} Factory interface for creating object instances (used internally by container for Transient services)</para>
    /// </summary>
    public interface IFactory
    {
        /// <summary>
        /// {RU} Создает новый экземпляр объекта
        /// <para>{EN} Creates a new object instance</para>
        /// </summary>
        /// <returns>{RU} Новый экземпляр объекта<para>{EN} New object instance</para></returns>
        object Create();
    }
}