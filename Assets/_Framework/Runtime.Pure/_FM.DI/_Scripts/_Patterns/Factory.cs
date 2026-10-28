using System;

namespace _FM.DI
{
    /// <summary>
    /// {RU} Реализация фабрики для создания экземпляров типа T (используется внутри контейнера)
    /// <para>{EN} Factory implementation for creating instances of type T (used internally by container)</para>
    /// </summary>
    /// <typeparam name="T">{RU} Тип создаваемых объектов<para>{EN} Type of objects to create</para></typeparam>
    public class Factory<T> : IFactory
    {
        private readonly Func<T> _factoryDelegate;

        /// <summary>
        /// {RU} Создает фабрику с указанной функцией-делегатом
        /// <para>{EN} Creates factory with specified delegate function</para>
        /// </summary>
        /// <param name="factoryDelegate">{RU} Функция для создания экземпляров<para>{EN} Function to create instances</para></param>
        public Factory(Func<T> factoryDelegate)
        {
            _factoryDelegate = factoryDelegate;
        }

        /// <summary>
        /// {RU} Создает новый экземпляр объекта типа T
        /// <para>{EN} Creates a new instance of object of type T</para>
        /// </summary>
        /// <returns>{RU} Новый экземпляр типа T<para>{EN} New instance of type T</para></returns>
        public object Create()
        {
            return _factoryDelegate();
        }
    }
}