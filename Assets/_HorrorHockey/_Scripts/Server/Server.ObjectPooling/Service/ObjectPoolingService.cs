using Microsoft.Extensions.Logging;
using Server.ObjectPooling.Core;
using Server.Shared.Configurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace Server.ObjectPooling.Service
{
    public class ObjectPoolingService<TObject> : IObjectPoolingService<TObject>, IDisposable where TObject : class, IPoolObj, new()
    {
        //private readonly ILogger<ObjectPoolingService<TObject>> _logger;
        private readonly ObjectPoolingConfiguration _config;
        private readonly List<IObjectSlot<TObject>> _objectPool = new();
        private readonly Queue<IObjectSlot<TObject>> _freeSlots = new();
        private readonly object _lockObject = new();

        private readonly System.Timers.Timer _checkTimer;
        private System.Timers.Timer? _deleteTimer;
        private bool _disposed = false;

        public ObjectPoolingService(
           //ILogger<ObjectPoolingService<TObject>> logger,
            ObjectPoolingConfiguration config)
        {
          //  _logger = logger;
            _config = config;

            AddNewSlot(_config.StartPoolSize);

            _checkTimer = new System.Timers.Timer(_config.DurationDelete * 1000); // Конвертируем секунды в миллисекунды
            _checkTimer.Elapsed += OnCheckTimerElapsed;
            _checkTimer.AutoReset = true;
            _checkTimer.Start();
        }

        #region Action Interface
        /// <summary>
        /// Получает свободный слот из пула. Если свободных слотов нет, автоматически добавляет новые.
        /// </summary>
        public IObjectSlot<TObject>? GetFreeSlot()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                // Сначала пытаемся взять из очереди свободных слотов
                while (_freeSlots.Count > 0)
                {
                    var slot = _freeSlots.Dequeue();

                    if (!slot.IsUse && _objectPool.Contains(slot))
                    {
                        return slot;
                    }
                }

                // Если очередь пуста, ищем в основном списке
                var freeSlot = FindFreeSlotInPool();

                // Если свободных слотов нет, добавляем новые автоматически
                if (freeSlot == null)
                {
                    TryAddSlotsIfNeeded();
                    freeSlot = FindFreeSlotInPool();
                }

                return freeSlot;
            }
        }

        /// <summary>
        /// Освобождает слот и возвращает его в пул свободных слотов
        /// </summary>
        public void ReleaseSlot(int index)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                if (!ValidationIndex(index))
                    return;

                var slot = _objectPool[index];
                if (slot.IsUse)
                {
                    slot.ResetObject();

                    // Добавляем в очередь свободных, если еще не там
                    if (!_freeSlots.Contains(slot))
                    {
                        _freeSlots.Enqueue(slot);
                    }
                }
            }
        }

        /// <summary>
        /// Освобождает слот по ссылке и возвращает его в пул свободных слотов
        /// </summary>
        public void ReleaseSlot(IObjectSlot<TObject> slot)
        {
            if (slot == null)
                throw new ArgumentNullException(nameof(slot));
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                if (_objectPool.Contains(slot) && slot.IsUse)
                {
                    slot.ResetObject();

                    // Добавляем в очередь свободных, если еще не там
                    if (!_freeSlots.Contains(slot))
                    {
                        _freeSlots.Enqueue(slot);
                    }
                }
            }
        }

        /// <summary>
        /// Обновляет данные объекта в слоте
        /// </summary>
        public void UpdateDataSlot(int index)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                if (!ValidationCurrentSlot(index, true))
                    return;

                _objectPool[index].TObj.UpdateData();
            }
        }

        /// <summary>
        /// Занимает слот объектом. Автоматически добавляет новые слоты при необходимости.
        /// </summary>
        public void OccupiedSlot(int index, TObject obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                if (!ValidationCurrentSlot(index, false))
                    return;

                var slot = _objectPool[index];
                slot.SetObject(obj);

                // Удаляем слот из очереди свободных, так как он теперь занят
                RemoveFromFreeQueue(slot);
            }
        }

        /// <summary>
        /// Занимает свободный слот объектом. Возвращает индекс занятого слота или -1 если не удалось.
        /// </summary>
        public int OccupiedFreeSlot(TObject obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                // Ищем свободный слот
                IObjectSlot<TObject>? slot = null;

                // Сначала пытаемся взять из очереди свободных слотов
                while (_freeSlots.Count > 0)
                {
                    var candidateSlot = _freeSlots.Dequeue();
                    if (!candidateSlot.IsUse && _objectPool.Contains(candidateSlot))
                    {
                        slot = candidateSlot;
                        break;
                    }
                }

                // Если очередь пуста, ищем в основном списке
                slot ??= FindFreeSlotInPool();

                // Если свободных слотов нет, добавляем новые автоматически
                if (slot == null)
                {
                    TryAddSlotsIfNeeded();
                    slot = FindFreeSlotInPool();
                }

                if (slot != null)
                {
                    slot.SetObject(obj);
                    RemoveFromFreeQueue(slot);
                    return slot.Id;
                }

                return -1;
            }
        }

        /// <summary>
        /// Получает список всех слотов для синхронизации
        /// </summary>
        public IReadOnlyList<IObjectSlot<TObject>> GetAllSlots()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                return _objectPool.ToList();
            }
        }

        /// <summary>
        /// Получает список слотов по указанным индексам
        /// </summary>
        public IReadOnlyList<IObjectSlot<TObject>> GetSlots(IEnumerable<int> indices)
        {
            if (indices == null)
                throw new ArgumentNullException(nameof(indices));
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                var result = new List<IObjectSlot<TObject>>();
                foreach (var index in indices)
                {
                    if (ValidationIndex(index))
                    {
                        result.Add(_objectPool[index]);
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// Обновляет данные в слотах на основе списка индексов
        /// </summary>
        public void UpdateSlotsData(IEnumerable<int> indices)
        {
            if (indices == null)
                throw new ArgumentNullException(nameof(indices));
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                foreach (var index in indices)
                {
                    if (ValidationCurrentSlot(index, true))
                    {
                        _objectPool[index].TObj.UpdateData();
                    }
                }
            }
        }

        /// <summary>
        /// Обновляет данные во всех занятых слотах
        /// </summary>
        public void UpdateAllOccupiedSlotsData()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                foreach (var slot in _objectPool)
                {
                    if (slot.IsUse)
                    {
                        // TObj никогда не null, так как создается в конструкторе
                        slot.TObj.UpdateData();
                    }
                }
            }
        }

        /// <summary>
        /// Освобождает несколько слотов по индексам
        /// </summary>
        public void ReleaseSlots(IEnumerable<int> indices)
        {
            if (indices == null)
                throw new ArgumentNullException(nameof(indices));
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                foreach (var index in indices)
                {
                    if (ValidationIndex(index))
                    {
                        var slot = _objectPool[index];
                        if (slot.IsUse)
                        {
                            // TObj никогда не null, так как создается в конструкторе
                            slot.ResetObject();

                            // Добавляем в очередь свободных, если еще не там
                            if (!_freeSlots.Contains(slot))
                            {
                                _freeSlots.Enqueue(slot);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Освобождает несколько слотов по ссылкам
        /// </summary>
        public void ReleaseSlots(IEnumerable<IObjectSlot<TObject>> slots)
        {
            if (slots == null)
                throw new ArgumentNullException(nameof(slots));
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                foreach (var slot in slots)
                {
                    if (slot != null && _objectPool.Contains(slot) && slot.IsUse)
                    {
                        // TObj никогда не null, так как создается в конструкторе
                        slot.ResetObject();

                        // Добавляем в очередь свободных, если еще не там
                        if (!_freeSlots.Contains(slot))
                        {
                            _freeSlots.Enqueue(slot);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Занимает несколько слотов объектами. Ключ - индекс слота, значение - объект для установки
        /// </summary>
        public void OccupiedSlots(Dictionary<int, TObject> slotData)
        {
            if (slotData == null)
                throw new ArgumentNullException(nameof(slotData));
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                foreach (var kvp in slotData)
                {
                    if (kvp.Value != null && ValidationCurrentSlot(kvp.Key, false))
                    {
                        var slot = _objectPool[kvp.Key];
                        slot.SetObject(kvp.Value);
                        RemoveFromFreeQueue(slot);
                    }
                }
            }
        }

        /// <summary>
        /// Занимает несколько свободных слотов объектами. Возвращает список ID занятых слотов
        /// </summary>
        public List<int> OccupiedFreeSlots(IEnumerable<TObject> objects)
        {
            if (objects == null)
                throw new ArgumentNullException(nameof(objects));
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);

            lock (_lockObject)
            {
                var occupiedIds = new List<int>();

                foreach (var obj in objects)
                {
                    if (obj == null)
                        continue;

                    // Ищем свободный слот
                    IObjectSlot<TObject>? slot = null;

                    // Сначала пытаемся взять из очереди свободных слотов
                    while (_freeSlots.Count > 0)
                    {
                        var candidateSlot = _freeSlots.Dequeue();
                        if (!candidateSlot.IsUse && _objectPool.Contains(candidateSlot))
                        {
                            slot = candidateSlot;
                            break;
                        }
                    }

                    // Если очередь пуста, ищем в основном списке
                    slot ??= FindFreeSlotInPool();

                    // Если свободных слотов нет, добавляем новые автоматически
                    if (slot == null)
                    {
                        TryAddSlotsIfNeeded();
                        slot = FindFreeSlotInPool();
                    }

                    if (slot != null)
                    {
                        slot.SetObject(obj);
                        RemoveFromFreeQueue(slot);
                        occupiedIds.Add(slot.Id);
                    }
                }

                return occupiedIds;
            }
        }
        #endregion

        #region Timer handlers
        /// <summary>
        /// Обработчик таймера проверки состояния (Таймер 1)
        /// </summary>
        private void OnCheckTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                if (_disposed)
                    return;

                var totalSlots = _objectPool.Count;
                var emptySlots = _objectPool.Count(slot => !slot.IsUse);

                if (totalSlots <= _config.StartPoolSize)
                {
                    return;
                }

                var emptyPercentage = (float)emptySlots / totalSlots;

                if (emptyPercentage > _config.PercentageToReduce)
                {
                    if (_deleteTimer != null)
                    {
                   //     _logger.LogWarning("Delete timer already exists. Skipping creation.");
                        return;
                    }

                    _checkTimer.Stop();

                    _deleteTimer = new System.Timers.Timer(_config.DurationDelete * 1000);
                    _deleteTimer.Elapsed += OnDeleteTimerElapsed;
                    _deleteTimer.AutoReset = false;
                    _deleteTimer.Start();
                }
            }
        }

        /// <summary>
        /// Обработчик таймера удаления (Таймер 2)
        /// </summary>
        private void OnDeleteTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                if (_disposed)
                    return;

                RemoveSlot();

                if (_deleteTimer != null)
                {
                    _deleteTimer.Stop();
                    _deleteTimer.Dispose();
                    _deleteTimer = null;
                }

                if (!_disposed)
                {
                    _checkTimer.Start();
                }
            }
        }
        #endregion

        #region Private helper
        /// <summary>
        /// Добавляет новые слоты при необходимости (приватный метод, вызывается автоматически)
        /// </summary>
        private void AddNewSlot(int count)
        {
            var currentCount = _objectPool.Count;
            for (int i = 0; i < count; i++)
            {
                AddSlot(currentCount + i);
            }
        }

        /// <summary>
        /// Проверяет необходимость добавления слотов и добавляет их автоматически
        /// </summary>
        private void TryAddSlotsIfNeeded()
        {
            var totalSlots = _objectPool.Count;

            // Если достигнут максимальный размер, не добавляем
            if (totalSlots >= _config.MaxPoolSize)
            {
          //      _logger.LogWarning(
        //            "Pool reached maximum size ({MaxSize}). Cannot add more slots.",
        //            _config.MaxPoolSize);
                return;
            }

            // Вычисляем количество слотов для добавления
            var slotsToAdd = (int)(totalSlots * _config.PercentageAdd);

            // Минимум 1 слот, если пул не пустой
            if (slotsToAdd < 1 && totalSlots > 0)
            {
                slotsToAdd = 1;
            }

            // Проверяем, не превысим ли максимальный размер
            if (totalSlots + slotsToAdd > _config.MaxPoolSize)
            {
                slotsToAdd = _config.MaxPoolSize - totalSlots;
            }

            if (slotsToAdd > 0)
            {
                AddNewSlot(slotsToAdd);
            }
        }

        private void RemoveSlot()
        {
            var totalSlots = _objectPool.Count;
            var emptySlots = _objectPool.Count(slot => !slot.IsUse);

            if (totalSlots <= _config.StartPoolSize)
            {
                return;
            }

            var emptyPercentage = (float)emptySlots / totalSlots;

            if (emptyPercentage > _config.PercentageToReduce)
            {
                var slotsToRemove = (int)(totalSlots * _config.PercentageReduction);

                var minSlotsToKeep = _config.StartPoolSize;
                var maxSlotsToRemove = totalSlots - minSlotsToKeep;

                if (slotsToRemove > maxSlotsToRemove)
                {
                    slotsToRemove = maxSlotsToRemove;
                }

                var removedCount = 0;
                for (int i = _objectPool.Count - 1; i >= 0 && removedCount < slotsToRemove; i--)
                {
                    if (!_objectPool[i].IsUse)
                    {
                        var slotToRemove = _objectPool[i];

                        // Удаляем слот из очереди свободных, если он там есть
                        RemoveFromFreeQueue(slotToRemove);

                        // Удаляем из основного списка
                        _objectPool.RemoveAt(i);
                        removedCount++;
                    }
                }
            }
        }

        private void AddSlot(int index)
        {
            var iObj = new ObjectSlot<TObject>(index) as IObjectSlot<TObject>;
            _objectPool.Add(iObj);

            // Новый слот свободен, добавляем в очередь
            _freeSlots.Enqueue(iObj);
        }

        /// <summary>
        /// Ищет свободный слот в основном пуле и добавляет найденные в очередь свободных
        /// </summary>
        private IObjectSlot<TObject>? FindFreeSlotInPool()
        {
            for (int i = 0; i < _objectPool.Count; i++)
            {
                var slot = _objectPool[i];
                if (!slot.IsUse)
                {
                    // Добавляем найденные свободные слоты в очередь для будущих запросов
                    if (!_freeSlots.Contains(slot))
                    {
                        _freeSlots.Enqueue(slot);
                    }

                    // Возвращаем первый найденный
                    return slot;
                }
            }

            return null;
        }

        /// <summary>
        /// Удаляет слот из очереди свободных слотов
        /// </summary>
        private void RemoveFromFreeQueue(IObjectSlot<TObject> slot)
        {
            // Создаем временную очередь без удаляемого слота
            var tempQueue = new Queue<IObjectSlot<TObject>>();

            while (_freeSlots.Count > 0)
            {
                var currentSlot = _freeSlots.Dequeue();
                if (currentSlot != slot)
                {
                    tempQueue.Enqueue(currentSlot);
                }
            }

            // Восстанавливаем очередь
            while (tempQueue.Count > 0)
            {
                _freeSlots.Enqueue(tempQueue.Dequeue());
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                if (_disposed)
                    return;

                _disposed = true;

                _checkTimer?.Stop();
                _checkTimer?.Dispose();

                _deleteTimer?.Stop();
                _deleteTimer?.Dispose();
                _deleteTimer = null;

                GC.SuppressFinalize(this);
            }
        }
        #endregion

        #region Validation
        private bool ValidationCurrentSlot(int index, bool isUse)
        {
            if (!ValidationIndex(index) || !ValidationSlot(index, isUse))
                return false;

            return true;
        }

        private bool ValidationIndex(int index)
        {
            if (index < 0 || index >= _objectPool.Count)
            {
           //     _logger.LogError(
          //          "Invalid slot index: {Index}. Pool size: {PoolSize}",
         //           index,
         //           _objectPool.Count);
                return false;
            }
            return true;
        }

        private bool ValidationSlot(int index, bool isUse)
        {
            var slot = _objectPool[index];

            // TObj никогда не null, так как создается в конструкторе ObjectSlot
            // Проверка на null не требуется для минимальной аллокации

            if (slot.IsUse != isUse)
            {
                var expectedState = isUse ? "occupied" : "free";
                var actualState = slot.IsUse ? "occupied" : "free";
              //  _logger.LogError(
          //          "Slot {Index} state mismatch. Expected: {ExpectedState}, Actual: {ActualState}",
          //          index,
          //          expectedState,
         //           actualState);
                return false;
            }

            return true;
        }
        #endregion
    }
}