using System;

namespace _FM.DI
{
    /// <summary>
    /// {RU} Упрощённый API для регистрации и разрешения. Модули используют вместо IServiceContainer.
    /// <para>{EN} Simplified API for registration and resolution. Modules use instead of IServiceContainer.</para>
    /// </summary>
    public interface IDIManager
    {
        void RegisterSingleton<TInterface, TImplementation>() where TImplementation : TInterface;
        void RegisterSingleton<T>(T instance);
        void RegisterTransient<TInterface, TImplementation>() where TImplementation : TInterface;
        void RegisterTransient<T>(Func<T> factory);
        T Resolve<T>();
        bool TryResolve<T>(out T result);
    }
}