namespace Server.Shared.Configurations
{
    /// <summary>
    /// Интерфейс для представления слота в пуле объектов
    /// </summary>
    public interface IObjectSlot<TObject> where TObject : class
    {
        /// <summary>
        /// Уникальный идентификатор слота
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Флаг, указывающий, занят ли слот
        /// </summary>
        bool IsUse { get; }

        /// <summary>
        /// Объект, хранящийся в слоте
        /// </summary>
        TObject TObj { get; set; }

        /// <summary>
        /// Устанавливает объект в слот
        /// </summary>
        void SetObject(TObject obj);

        /// <summary>
        /// Сбрасывает слот, освобождая его
        /// </summary>
        void ResetObject();

        /// <summary>
        /// Обновляет данные объекта в слоте
        /// </summary>
        void UpdateData();
    }
}