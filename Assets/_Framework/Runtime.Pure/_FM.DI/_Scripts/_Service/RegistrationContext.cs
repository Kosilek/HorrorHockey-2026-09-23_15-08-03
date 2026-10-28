using System;
using FM.General.Core.Interfaces.Contracts;

namespace _FM.DI
{
    internal class RegistrationContext : IRegistrationContext
    {
        private readonly DIManagerOptions _options;

        public RegistrationContext(DIManagerOptions options) => _options = options;

        public void RegisterSingleton<TInterface, TImplementation>() where TImplementation : TInterface =>
            _options.ExternalRegistrations.Add((di, _) => di.RegisterSingleton<TInterface, TImplementation>());

        public void RegisterSingleton<T>(T instance) =>
            _options.ExternalRegistrations.Add((di, _) => di.RegisterSingleton(instance));

        public void RegisterTransient<TInterface, TImplementation>() where TImplementation : TInterface =>
            _options.ExternalRegistrations.Add((di, _) => di.RegisterTransient<TInterface, TImplementation>());

        public void RegisterTransient<T>(Func<T> factory) =>
            _options.ExternalRegistrations.Add((di, _) => di.RegisterTransient(factory));

        public bool HasAssembly(string name) => _options.HasAssembly(name);
    }
}
