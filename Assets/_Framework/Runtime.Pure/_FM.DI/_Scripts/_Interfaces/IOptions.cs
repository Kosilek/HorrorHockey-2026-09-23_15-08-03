using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace _FM.DI
{
    /// <summary>
    /// {RU} Интерфейс для паттерна Options, предоставляющий доступ к конфигурационным данным
    /// <para>{EN} Options pattern interface providing access to configuration data</para>
    /// </summary>
    /// <typeparam name="T">{RU} Тип конфигурационных данных (должен быть классом)<para>{EN} Configuration data type (must be a class)</para></typeparam>
    public interface IOptions<T> where T : class
    {
        /// <summary>
        /// {RU} Значение конфигурации
        /// <para>{EN} Configuration value</para>
        /// </summary>
        T Value { get; }
    }
}
