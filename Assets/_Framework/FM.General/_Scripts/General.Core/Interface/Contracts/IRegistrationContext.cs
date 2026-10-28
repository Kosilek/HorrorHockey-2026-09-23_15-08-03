using System;

namespace FM.General.Core.Interfaces.Contracts
{
    /// <summary>
    /// {RU} Контекст регистрации в DI. Передаётся в IRegisterInDI. Модули не зависят от _FM.DI.
    /// <para>{EN} DI registration context. Passed to IRegisterInDI. Modules don't depend on _FM.DI.</para>
    /// </summary>
    public interface IRegistrationContext
    {
        void RegisterSingleton<TInterface, TImplementation>() where TImplementation : TInterface;
        void RegisterSingleton<T>(T instance);
        void RegisterTransient<TInterface, TImplementation>() where TImplementation : TInterface;
        void RegisterTransient<T>(Func<T> factory);
        bool HasAssembly(string name);
    }
}
