using System;
using System.Collections.Generic;
using System.Linq;

namespace _FM.DI
{
    public partial class DIManager : IDIManager
    {
        private static readonly List<Action<DIManagerOptions>> _moduleInits = new();

        /// <summary>
        /// {RU} Регистрирует инициализатор модуля. Вызывать из static field initializer в каждой partial-части:
        /// <c>private static readonly bool _ = Register(InitLogger, 10);</c>
        /// Порядок: priority (меньше = раньше). Без priority — выполняется последним.
        /// <para>{EN} Registers module initializer. Call from static field initializer in each partial part.</para>
        /// </summary>
        /// <returns>true — для использования в static field initializer</returns>
        public static bool Register(Action<DIManagerOptions> init, int priority = int.MaxValue)
        {
            _moduleInits.Add(init);
            _priorities[init] = priority;
            return true;
        }

        private static readonly Dictionary<Action<DIManagerOptions>, int> _priorities = new();

        private readonly IServiceContainer _rootContext;
        private readonly IServiceContainer _matchContext;
        private readonly IServiceContainer _menuContext;

        public DIManager()
        {
            _rootContext = new DIContainerService();
            _matchContext = new DIContainerService
            {
                FallbackContainer = _rootContext as DIContainerService,
                DefaultPolicy = ResolutionPolicy.TryGlobalFallback
            };
            _menuContext = new DIContainerService
            {
                FallbackContainer = _rootContext as DIContainerService,
                DefaultPolicy = ResolutionPolicy.TryGlobalFallback
            };
        }

        public void Initialize(DIManagerOptions diManagerOptions)
        {
            diManagerOptions.Container = _rootContext;

            var ordered = _moduleInits.OrderBy(i => _priorities.TryGetValue(i, out var p) ? p : int.MaxValue);
            foreach (var init in ordered)
                init(diManagerOptions);

            foreach (var reg in diManagerOptions.ExternalRegistrations)
                reg(this, diManagerOptions);

            // Регистрации уже в root. Match и menu пока пустые, сервисы туда не пишутся.
            UnityInjectService.Instance = new UnityInjectService(_rootContext);
        }

        public void RegisterSingleton<TInterface, TImplementation>() where TImplementation : TInterface =>
            _rootContext.AddSingleton<TInterface, TImplementation>();

        public void RegisterSingleton<T>(T instance) =>
            _rootContext.AddSingleton(instance);

        public void RegisterTransient<TInterface, TImplementation>() where TImplementation : TInterface =>
            _rootContext.AddTransient<TInterface, TImplementation>();

        public void RegisterTransient<T>(Func<T> factory) =>
            _rootContext.AddTransient(factory);

        public T Resolve<T>() =>
            _rootContext.Resolve<T>();

        public bool TryResolve<T>(out T result) =>
            _rootContext.TryResolve(out result);
    }
}