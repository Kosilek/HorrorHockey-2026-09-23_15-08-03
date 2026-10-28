using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace _FM.DI
{
    /// <summary>
    /// {RU} Основной интерфейс DI контейнера, объединяющий регистрацию и разрешение зависимостей
    /// <para>{EN} Main DI container interface combining registration and resolution of dependencies</para>
    /// </summary>
    public interface IServiceContainer : IRegistertration, IResolver
    {
        /// <summary>
        /// {RU} Инициализирует контейнер и разрешает зависимости конструкторов для всех зарегистрированных сервисов
        /// <para>{EN} Initializes container and resolves constructor dependencies for all registered services</para>
        /// </summary>
        void InitializeService();

        /// <summary>
        /// {RU} Инъекция зависимостей в уже созданный объект через методы с атрибутом [InjectMethod]
        /// <para>{EN} Injects dependencies into already created object through methods with [InjectMethod] attribute</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип объекта<para>{EN} Object type</para></typeparam>
        /// <param name="instance">{RU} Экземпляр объекта<para>{EN} Object instance</para></param>
        void InjectDependencies<T>(T instance);

        ResolutionPolicy DefaultPolicy { get; set; }
        DIContainerService FallbackContainer { get; set; }

        void Clear();
    }

    /// <summary>
    /// {RU} Интерфейс для регистрации сервисов в DI контейнере
    /// <para>{EN} Interface for registering services in DI container</para>
    /// </summary>
    public interface IRegistertration
    {
        /// <summary>
        /// {RU} Регистрирует transient сервис с фабрикой. Новый экземпляр создается при каждом вызове Resolve
        /// <para>{EN} Registers transient service with factory. New instance is created on each Resolve call</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип сервиса<para>{EN} Service type</para></typeparam>
        /// <param name="factory">{RU} Функция-фабрика для создания экземпляров<para>{EN} Factory function to create instances</para></param>
        void AddTransient<T>(Func<T> factory);

        /// <summary>
        /// {RU} Регистрирует transient сервис по интерфейсу и реализации
        /// <para>{EN} Registers transient service by interface and implementation</para>
        /// </summary>
        /// <typeparam name="TInterface">{RU} Тип интерфейса<para>{EN} Interface type</para></typeparam>
        /// <typeparam name="TImplementation">{RU} Тип реализации<para>{EN} Implementation type</para></typeparam>
        void AddTransient<TInterface, TImplementation>() where TImplementation : TInterface;

        /// <summary>
        /// {RU} Регистрирует singleton сервис. Один экземпляр используется для всех вызовов Resolve
        /// <para>{EN} Registers singleton service. Single instance is used for all Resolve calls</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип сервиса<para>{EN} Service type</para></typeparam>
        /// <param name="instance">{RU} Экземпляр сервиса<para>{EN} Service instance</para></param>
        void AddSingleton<T>(T instance);

        /// <summary>
        /// {RU} Регистрирует singleton сервис по интерфейсу и реализации
        /// <para>{EN} Registers singleton service by interface and implementation</para>
        /// </summary>
        /// <typeparam name="TInterface">{RU} Тип интерфейса<para>{EN} Interface type</para></typeparam>
        /// <typeparam name="TImplementation">{RU} Тип реализации<para>{EN} Implementation type</para></typeparam>
        void AddSingleton<TInterface, TImplementation>() where TImplementation : TInterface;

        /// <summary>
        /// {RU} Регистрирует singleton сервис с именем для разрешения именованных зависимостей
        /// <para>{EN} Registers singleton service with name for named dependency resolution</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип сервиса<para>{EN} Service type</para></typeparam>
        /// <param name="instance">{RU} Экземпляр сервиса<para>{EN} Service instance</para></param>
        /// <param name="name">{RU} Имя регистрации<para>{EN} Registration name</para></param>
        void AddSingleton<T>(T instance, string name);

        /// <summary>
        /// {RU} Регистрирует transient сервис с именем для разрешения именованных зависимостей
        /// <para>{EN} Registers transient service with name for named dependency resolution</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип сервиса<para>{EN} Service type</para></typeparam>
        /// <param name="factory">{RU} Функция-фабрика для создания экземпляров<para>{EN} Factory function to create instances</para></param>
        /// <param name="name">{RU} Имя регистрации<para>{EN} Registration name</para></param>
        void AddTransient<T>(Func<T> factory, string name);

        /// <summary>
        /// {RU} Регистрирует transient сервис по интерфейсу и реализации с именем
        /// <para>{EN} Registers transient service by interface and implementation with name</para>
        /// </summary>
        /// <typeparam name="TInterface">{RU} Тип интерфейса<para>{EN} Interface type</para></typeparam>
        /// <typeparam name="TImplementation">{RU} Тип реализации<para>{EN} Implementation type</para></typeparam>
        /// <param name="name">{RU} Имя регистрации<para>{EN} Registration name</para></param>
        void AddTransient<TInterface, TImplementation>(string name) where TImplementation : TInterface;
    }

    /// <summary>
    /// {RU} Интерфейс для разрешения зависимостей из DI контейнера
    /// <para>{EN} Interface for resolving dependencies from DI container</para>
    /// </summary>
    public interface IResolver
    {
        /// <summary>
        /// {RU} Разрешает зависимость по типу с возможностью указания политики разрешения
        /// <para>{EN} Resolves dependency by type with optional resolution policy</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип зависимости<para>{EN} Dependency type</para></typeparam>
        /// <param name="resolutionPolicy">{RU} Политика разрешения (если null, используется DefaultPolicy)<para>{EN} Resolution policy (if null, DefaultPolicy is used)</para></param>
        /// <returns>{RU} Экземпляр зависимости<para>{EN} Dependency instance</para></returns>
        T Resolve<T>(ResolutionPolicy? resolutionPolicy = ResolutionPolicy.ThrowException);

        /// <summary>
        /// {RU} Пытается разрешить зависимость без выброса исключения
        /// <para>{EN} Tries to resolve dependency without throwing exception</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип зависимости<para>{EN} Dependency type</para></typeparam>
        /// <param name="result">{RU} Результат разрешения<para>{EN} Resolution result</para></param>
        /// <returns>{RU} true если зависимость найдена, иначе false<para>{EN} true if dependency is found, otherwise false</para></returns>
        bool TryResolve<T>(out T result);

        /// <summary>
        /// {RU} Разрешает все зарегистрированные реализации указанного типа
        /// <para>{EN} Resolves all registered implementations of specified type</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип зависимости<para>{EN} Dependency type</para></typeparam>
        /// <returns>{RU} Коллекция всех найденных экземпляров<para>{EN} Collection of all found instances</para></returns>
        IEnumerable<T> ResolveAll<T>();

        /// <summary>
        /// {RU} Разрешает именованную зависимость по типу и имени
        /// <para>{EN} Resolves named dependency by type and name</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип зависимости<para>{EN} Dependency type</para></typeparam>
        /// <param name="name">{RU} Имя регистрации<para>{EN} Registration name</para></param>
        /// <param name="resolutionPolicy">{RU} Политика разрешения<para>{EN} Resolution policy</para></param>
        /// <returns>{RU} Экземпляр зависимости<para>{EN} Dependency instance</para></returns>
        T Resolve<T>(string name, ResolutionPolicy? resolutionPolicy = ResolutionPolicy.ThrowException);
    }
}
