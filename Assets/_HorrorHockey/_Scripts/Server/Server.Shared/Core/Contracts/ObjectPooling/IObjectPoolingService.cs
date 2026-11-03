using System;
using System.Collections.Generic;

namespace Server.Shared.Configurations
{
    /// <summary>
    /// Интерфейс для управления пулом объектов
    /// </summary>
    public interface IObjectPoolingService<TObject> : IDisposable where TObject : class, IPoolObj, new()
    {
        /// <summary>
        /// Получает свободный слот из пула. Если свободных слотов нет, автоматически добавляет новые.
        /// </summary>
        IObjectSlot<TObject>? GetFreeSlot();

        /// <summary>
        /// Освобождает слот и возвращает его в пул свободных слотов
        /// </summary>
        void ReleaseSlot(int index);

        /// <summary>
        /// Освобождает слот по ссылке и возвращает его в пул свободных слотов
        /// </summary>
        void ReleaseSlot(IObjectSlot<TObject> slot);

        /// <summary>
        /// Обновляет данные объекта в слоте
        /// </summary>
        void UpdateDataSlot(int index);

        /// <summary>
        /// Занимает слот объектом. Автоматически добавляет новые слоты при необходимости.
        /// </summary>
        void OccupiedSlot(int index, TObject obj);

        /// <summary>
        /// Занимает свободный слот объектом. Возвращает индекс занятого слота или -1 если не удалось.
        /// </summary>
        int OccupiedFreeSlot(TObject obj);

        /// <summary>
        /// Получает список всех слотов для синхронизации
        /// </summary>
        IReadOnlyList<IObjectSlot<TObject>> GetAllSlots();

        /// <summary>
        /// Получает список слотов по указанным индексам
        /// </summary>
        IReadOnlyList<IObjectSlot<TObject>> GetSlots(IEnumerable<int> indices);

        /// <summary>
        /// Обновляет данные в слотах на основе списка индексов
        /// </summary>
        void UpdateSlotsData(IEnumerable<int> indices);

        /// <summary>
        /// Обновляет данные во всех занятых слотах
        /// </summary>
        void UpdateAllOccupiedSlotsData();

        /// <summary>
        /// Освобождает несколько слотов по индексам
        /// </summary>
        void ReleaseSlots(IEnumerable<int> indices);

        /// <summary>
        /// Освобождает несколько слотов по ссылкам
        /// </summary>
        void ReleaseSlots(IEnumerable<IObjectSlot<TObject>> slots);

        /// <summary>
        /// Занимает несколько слотов объектами. Ключ - индекс слота, значение - объект для установки
        /// </summary>
        void OccupiedSlots(Dictionary<int, TObject> slotData);

        /// <summary>
        /// Занимает несколько свободных слотов объектами. Возвращает список ID занятых слотов
        /// </summary>
        List<int> OccupiedFreeSlots(IEnumerable<TObject> objects);
    }
}
