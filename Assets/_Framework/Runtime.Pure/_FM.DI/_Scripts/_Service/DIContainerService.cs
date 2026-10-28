using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace _FM.DI
{
    /// <summary>
    /// {RU} Реализация DI контейнера с поддержкой инъекции через конструктор и управлением жизненным циклом
    /// <para>{EN} DI container implementation with constructor injection support and lifetime management</para>
    /// </summary>
    public class DIContainerService : IServiceContainer
    {
        private Dictionary<Type, object> _containersTransient = new();
        private Dictionary<Type, object> _containersSingleton = new();
        private Dictionary<(Type, string), object> _namedTransient = new();
        private Dictionary<(Type, string), object> _namedSingleton = new();

        /// <summary>
        /// {RU} Политика разрешения зависимостей по умолчанию, используемая когда не указана явно
        /// <para>{EN} Default dependency resolution policy used when not explicitly specified</para>
        /// </summary>
        public ResolutionPolicy DefaultPolicy { get; set; } = ResolutionPolicy.ThrowException;

        /// <summary>
        /// {RU} Резервный контейнер для цепочки разрешения зависимостей при использовании политики TryGlobalFallback
        /// <para>{EN} Fallback container for dependency resolution chain when using TryGlobalFallback policy</para>
        /// </summary>
        public DIContainerService FallbackContainer { get; set; }

        #region API Interface
        /// <summary>
        /// {RU} Инициализирует контейнер и разрешает все зависимости конструкторов
        /// <para>{EN} Initializes container and resolves all constructor dependencies</para>
        /// </summary>
        public void InitializeService()
        {
            InitializeContainer(_containersTransient);
            InitializeContainer(_containersSingleton);
        }

        /// <summary>
        /// {RU} Регистрирует singleton экземпляр в контейнере
        /// <para>{EN} Registers singleton instance in container</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип сервиса<para>{EN} Service type</para></typeparam>
        /// <param name="instance">{RU} Экземпляр для регистрации<para>{EN} Instance to register</para></param>
        public void AddSingleton<T>(T instance)
        {
            if (AddValidation<T>())
            {
                _containersSingleton.Add(typeof(T), instance);
            }
            else
            {
                throw new ArgumentException($"An attempt to create a duplicate key. Enter the key => {typeof(T)}");
            }
        }

        /// <summary>
        /// {RU} Регистрирует singleton сервис по интерфейсу и реализации
        /// <para>{EN} Registers singleton service by interface and implementation</para>
        /// </summary>
        /// <typeparam name="TInterface">{RU} Тип интерфейса<para>{EN} Interface type</para></typeparam>
        /// <typeparam name="TImplementation">{RU} Тип реализации (должен иметь конструктор по умолчанию)<para>{EN} Implementation type (must have default constructor)</para></typeparam>
        public void AddSingleton<TInterface, TImplementation>() where TImplementation : TInterface
        {
            if (AddValidation<TInterface>())
            {
                _containersSingleton.Add(typeof(TInterface), typeof(TImplementation));
            }
            else
            {
                throw new ArgumentException($"An attempt to create a duplicate key. Enter the key => {typeof(TInterface)}");
            }
        }

        /// <summary>
        /// {RU} Регистрирует transient сервис с фабричной функцией
        /// <para>{EN} Registers transient service with factory function</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип сервиса<para>{EN} Service type</para></typeparam>
        /// <param name="factory">{RU} Функция-фабрика для создания новых экземпляров<para>{EN} Factory function to create new instances</para></param>
        public void AddTransient<T>(Func<T> factory)
        {
            if (AddValidation<T>())
            {
                _containersTransient[typeof(T)] = new Factory<T>(factory);
            }
            else
            {
                throw new ArgumentException($"An attempt to create a duplicate key. Enter the key => {typeof(T)}");
            }
        }

        /// <summary>
        /// {RU} Регистрирует transient сервис по интерфейсу и реализации
        /// <para>{EN} Registers transient service by interface and implementation</para>
        /// </summary>
        /// <typeparam name="TInterface">{RU} Тип интерфейса<para>{EN} Interface type</para></typeparam>
        /// <typeparam name="TImplementation">{RU} Тип реализации (должен иметь конструктор по умолчанию)<para>{EN} Implementation type (must have default constructor)</para></typeparam>
        public void AddTransient<TInterface, TImplementation>() where TImplementation : TInterface
        {
            if (AddValidation<TInterface>())
            {
                _containersTransient[typeof(TInterface)] = typeof(TImplementation);
            }
            else
            {
                throw new ArgumentException($"An attempt to create a duplicate key. Enter the key => {typeof(TInterface)}");
            }
        }

        /// <summary>
        /// {RU} Регистрирует singleton сервис с именем для разрешения именованных зависимостей
        /// <para>{EN} Registers singleton service with name for named dependency resolution</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип сервиса<para>{EN} Service type</para></typeparam>
        /// <param name="instance">{RU} Экземпляр сервиса<para>{EN} Service instance</para></param>
        /// <param name="name">{RU} Имя регистрации<para>{EN} Registration name</para></param>
        public void AddSingleton<T>(T instance, string name)
        {
            var key = (typeof(T), name);
            if (_namedSingleton.ContainsKey(key))
            {
                throw new ArgumentException($"An attempt to create a duplicate named key. Type => {typeof(T)}, Name => {name}");
            }
            _namedSingleton.Add(key, instance);
        }

        /// <summary>
        /// {RU} Регистрирует transient сервис с именем для разрешения именованных зависимостей
        /// <para>{EN} Registers transient service with name for named dependency resolution</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип сервиса<para>{EN} Service type</para></typeparam>
        /// <param name="factory">{RU} Функция-фабрика для создания экземпляров<para>{EN} Factory function to create instances</para></param>
        /// <param name="name">{RU} Имя регистрации<para>{EN} Registration name</para></param>
        public void AddTransient<T>(Func<T> factory, string name)
        {
            var key = (typeof(T), name);
            if (_namedTransient.ContainsKey(key))
            {
                throw new ArgumentException($"An attempt to create a duplicate named key. Type => {typeof(T)}, Name => {name}");
            }
            _namedTransient.Add(key, new Factory<T>(factory));
        }

        /// <summary>
        /// {RU} Регистрирует transient сервис по интерфейсу и реализации с именем
        /// <para>{EN} Registers transient service by interface and implementation with name</para>
        /// </summary>
        /// <typeparam name="TInterface">{RU} Тип интерфейса<para>{EN} Interface type</para></typeparam>
        /// <typeparam name="TImplementation">{RU} Тип реализации<para>{EN} Implementation type</para></typeparam>
        /// <param name="name">{RU} Имя регистрации<para>{EN} Registration name</para></param>
        public void AddTransient<TInterface, TImplementation>(string name) where TImplementation : TInterface
        {
            var key = (typeof(TInterface), name);
            if (_namedTransient.ContainsKey(key))
            {
                throw new ArgumentException($"An attempt to create a duplicate named key. Type => {typeof(TInterface)}, Name => {name}");
            }
            _namedTransient.Add(key, typeof(TImplementation));
        }

        /// <summary>
        /// {RU} Разрешает зависимость по типу
        /// <para>{EN} Resolves dependency by type</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип зависимости для разрешения<para>{EN} Dependency type to resolve</para></typeparam>
        /// <param name="policy">{RU} Политика разрешения (если null, используется DefaultPolicy)<para>{EN} Resolution policy (if null, DefaultPolicy is used)</para></param>
        /// <returns>{RU} Экземпляр зависимости<para>{EN} Dependency instance</para></returns>
        public T Resolve<T>(ResolutionPolicy? policy = null)
        {
            var type = typeof(T);
            var effectivePolicy = policy ?? DefaultPolicy;
            var result = (T)GetService(type, effectivePolicy);
            return result;
        }

        private object GetService(Type type, ResolutionPolicy policy)
        {

            if (_containersTransient.TryGetValue(type, out var service))
            {
                if (type.Name.Contains("HelperEnemyAI"))
                {
                }

                if (service is IFactory factory)
                {
                    var instance = factory.Create();
                    return instance;
                }
                if (service is Type serviceType)
                {
                    var instance = CreateInstanceWithDependencies(serviceType);
                    return instance;
                }
                // ВАЖНО: Для Transient сервисов не должно храниться экземпляров, только типы!
                // Если в словаре экземпляр, значит была ошибка регистрации.
                // Но мы все равно должны создавать новый экземпляр для Transient.
                // Попробуем создать новый экземпляр через CreateInstanceWithDependencies
                if (service != null && !(service is Type) && !(service is IFactory))
                {
                    var actualType = service.GetType(); 
                    // Создаем новый экземпляр на основе типа сохраненного экземпляра
                    var newInstance = CreateInstanceWithDependencies(actualType);
                    // НЕ заменяем в словаре, оставляем как есть, чтобы не ломать другие места
                    return newInstance;
                }
                return service;
            }

            if (_containersSingleton.TryGetValue(type, out var singleton))
            {
                if (singleton is Type singletonType)
                {
                    var instance = CreateInstanceWithDependencies(singletonType);
                    _containersSingleton[type] = instance;
                    return instance;
                }
                return singleton;
            }

            return HandleNotFound(type, policy);
        }

        private object HandleNotFound(Type type, ResolutionPolicy policy)
        {
            switch (policy)
            {
                case ResolutionPolicy.ReturnNull:
                    return null;

                case ResolutionPolicy.TryGlobalFallback:
                    if (FallbackContainer != null)
                    {
                        try
                        {
                            var fallbackResult = FallbackContainer.GetService(type, ResolutionPolicy.ReturnNull);
                            return fallbackResult;
                        }
                        catch
                        {
                            return null;
                        }
                    }
                    return null;

                case ResolutionPolicy.ThrowException:
                default:
                    throw new ArgumentException($"Type not registered: {type}");
            }
        }

        /// <summary>
        /// {RU} Разрешает все зарегистрированные реализации указанного типа
        /// <para>{EN} Resolves all registered implementations of specified type</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип зависимости<para>{EN} Dependency type</para></typeparam>
        /// <returns>{RU} Коллекция всех найденных экземпляров<para>{EN} Collection of all found instances</para></returns>
        public IEnumerable<T> ResolveAll<T>()
        {
            var results = new List<T>();

            results = GetServiceAll(results, _containersSingleton);
            results = GetServiceAll(results, _containersTransient);

            return results;
        }

        /// <summary>
        /// {RU} Пытается разрешить зависимость без выброса исключения
        /// <para>{EN} Tries to resolve dependency without throwing exception</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип зависимости<para>{EN} Dependency type</para></typeparam>
        /// <param name="result">{RU} Результат разрешения зависимости<para>{EN} Dependency resolution result</para></param>
        /// <returns>{RU} true если зависимость успешно разрешена, иначе false<para>{EN} true if dependency is successfully resolved, otherwise false</para></returns>
        public bool TryResolve<T>(out T result)
        {
            var type = typeof(T);
            result = (T)GetService(type);
            return result != null;
        }

        /// <summary>
        /// {RU} Разрешает именованную зависимость по типу и имени
        /// <para>{EN} Resolves named dependency by type and name</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип зависимости<para>{EN} Dependency type</para></typeparam>
        /// <param name="name">{RU} Имя регистрации<para>{EN} Registration name</para></param>
        /// <param name="resolutionPolicy">{RU} Политика разрешения<para>{EN} Resolution policy</para></param>
        /// <returns>{RU} Экземпляр зависимости<para>{EN} Dependency instance</para></returns>
        public T Resolve<T>(string name, ResolutionPolicy? resolutionPolicy = null)
        {
            var type = typeof(T);
            var effectivePolicy = resolutionPolicy ?? DefaultPolicy;
            var key = (type, name);

            // Сначала проверяем именованные регистрации
            if (_namedTransient.TryGetValue(key, out var transientService))
            {
                if (transientService is IFactory factory)
                {
                    return (T)factory.Create();
                }
                if (transientService is Type serviceType)
                {
                    return (T)CreateInstanceWithDependencies(serviceType);
                }
                return (T)transientService;
            }

            if (_namedSingleton.TryGetValue(key, out var singletonService))
            {
                if (singletonService is Type singletonType)
                {
                    var instance = CreateInstanceWithDependencies(singletonType);
                    _namedSingleton[key] = instance;
                    return (T)instance;
                }
                return (T)singletonService;
            }

            // Если именованная регистрация не найдена, используем стандартную логику
            return Resolve<T>(effectivePolicy);
        }
        #endregion

        #region Helper private
        private List<T> GetServiceAll<T>(List<T> results, Dictionary<Type, object> cont)
        {
            var targetType = typeof(T);

            foreach (var kvp in cont)
            {
                var valueType = kvp.Value is Type registeredType
      ? registeredType
      : kvp.Value.GetType();

                if (targetType.IsAssignableFrom(valueType))
                {
                    var instance = kvp.Value is Type instanceType
                        ? CreateInstanceWithDependencies(instanceType)
                        : kvp.Value;
                    results.Add((T)instance);
                }
            }
            return results;
        }

        private object GetService(Type type)
        {
            if (_containersTransient.TryGetValue(type, out var service))
            {
                if (service is IFactory factory)
                {
                    var instance = factory.Create();
                    return instance;
                }
                if (service is Type serviceType)
                {
                    var instance = CreateInstanceWithDependencies(serviceType);
                    return instance;
                }
                return service;
            }
            if (_containersSingleton.TryGetValue(type, out var singleton))
            {
                if (singleton is Type singletonType)
                {
                    var instance = CreateInstanceWithDependencies(singletonType);
                    _containersSingleton[type] = instance;
                    return instance;
                }
                return singleton;
            }
            throw new ArgumentException($"This type is not registered in the DI container. Injection is not possible. Type => {typeof(Type)}, {type.Name}");
        }

        private void InitializeContainer(Dictionary<Type, object> cont)
        {
            Dictionary<Type, object> services = new();
            HashSet<object> processedInstances = new();
            var keys = cont.Keys.ToArray();
            foreach (var key in keys)
            {

                var container = new KeyValuePair<Type, object>(key, cont[key]);

                if (!(container.Value is Type registeredType))
                {
                    if (processedInstances.Add(container.Value))
                    {
                        InjectDependencies(container.Value);
                    }
                    continue;
                }

                services.Clear();
                var type = registeredType;
                var constrs = type.GetConstructors();

                var constructor = AttributeField(constrs);
                ParameterInfo[] parameters = Array.Empty<ParameterInfo>();

                if (constructor != null)
                {
                    parameters = constructor.GetParameters();
                    InitializeConstructor(services, parameters, constructor);
                }
                else
                {
                    if (constrs.Length == 1)
                    {
                        parameters = constrs[0].GetParameters();
                        constructor = constrs[0];
                    }
                    else
                    {
                        var maxParams = int.MinValue;
                        int countParams;
                        foreach (var constr in constrs)
                        {
                            countParams = constr.GetParameters().Length;
                            if (countParams > maxParams)
                            {
                                maxParams = countParams;
                                constructor = constr;
                            }
                        }
                    }
                }
                var instance = InitializeConstructor(services, parameters, constructor);
                cont[container.Key] = instance;
            }
        }

        private object CreateInstanceWithDependencies(Type type)
        {
            var constrs = type.GetConstructors();
            var constructor = AttributeField(constrs);

            if (constructor == null)
            {
                if (constrs.Length == 1)
                {
                    constructor = constrs[0];
                }
                else
                {
                    // Выбираем конструктор с наибольшим количеством параметров
                    var maxParams = int.MinValue;
                    foreach (var constr in constrs)
                    {
                        var countParams = constr.GetParameters().Length;
                        if (countParams > maxParams)
                        {
                            maxParams = countParams;
                            constructor = constr;
                        }
                    }
                }
            }

            var parameters = constructor.GetParameters();
            object[] args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var paramType = param.ParameterType;

                // Проверяем наличие атрибута [InjectName] на параметре
                var injectNameAttr = param.GetCustomAttribute<InjectNameAttribute>();
                if (injectNameAttr != null && !string.IsNullOrEmpty(injectNameAttr.Name))
                {
                    // Разрешаем именованную зависимость
                    var key = (paramType, injectNameAttr.Name);

                    if (_namedTransient.TryGetValue(key, out var namedTransient))
                    {
                        if (namedTransient is IFactory factory)
                        {
                            args[i] = factory.Create();
                        }
                        else if (namedTransient is Type serviceType)
                        {
                            args[i] = CreateInstanceWithDependencies(serviceType);
                        }
                        else
                        {
                            args[i] = namedTransient;
                        }
                        continue;
                    }

                    if (_namedSingleton.TryGetValue(key, out var namedSingleton))
                    {
                        if (namedSingleton is Type singletonType)
                        {
                            var instanceName = CreateInstanceWithDependencies(singletonType);
                            _namedSingleton[key] = instanceName;
                            args[i] = instanceName;
                        }
                        else
                        {
                            args[i] = namedSingleton;
                        }
                        continue;
                    }

                    // Если именованная регистрация не найдена, пробуем стандартное разрешение
                }

                // Используем TryGlobalFallback для резолвинга зависимостей при создании Transient экземпляров
                // Это гарантирует, что зависимости будут найдены в текущем контейнере или FallbackContainer
                args[i] = GetService(paramType, ResolutionPolicy.TryGlobalFallback);
            }

            var instance = constructor.Invoke(args);
            return instance;
        }

        private ConstructorInfo AttributeField(ConstructorInfo[] constructor)
        {
            foreach (var constr in constructor)
            {
                var hasAttribute = constr.IsDefined(typeof(InjectConstructorAttribute), false);
                if (hasAttribute)
                {
                    return constr;
                }
            }
            return null;
        }

        private object InitializeConstructor(Dictionary<Type, object> services,
            ParameterInfo[] parameters, ConstructorInfo constructor)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var obj = GetService(parameters[i].ParameterType, DefaultPolicy);
                var objType = obj.GetType();
                if (!services.ContainsKey(objType))
                {
                    services.Add(key: objType, value: obj);
                }
            }

            var parameterInfos = constructor.GetParameters();

            object[] args = new object[parameterInfos.Length];

            for (int i = 0; i < parameterInfos.Length; i++)
            {
                var param = parameterInfos[i];
                var paramType = param.ParameterType;

                // Проверяем наличие атрибута [InjectName] на параметре
                var injectNameAttr = param.GetCustomAttribute<InjectNameAttribute>();
                if (injectNameAttr != null && !string.IsNullOrEmpty(injectNameAttr.Name))
                {
                    // Разрешаем именованную зависимость
                    var key = (paramType, injectNameAttr.Name);

                    if (_namedTransient.TryGetValue(key, out var namedTransient))
                    {
                        if (namedTransient is IFactory factory)
                        {
                            args[i] = factory.Create();
                        }
                        else if (namedTransient is Type serviceType)
                        {
                            args[i] = CreateInstanceWithDependencies(serviceType);
                        }
                        else
                        {
                            args[i] = namedTransient;
                        }
                        continue;
                    }

                    if (_namedSingleton.TryGetValue(key, out var namedSingleton))
                    {
                        if (namedSingleton is Type singletonType)
                        {
                            var instance = CreateInstanceWithDependencies(singletonType);
                            _namedSingleton[key] = instance;
                            args[i] = instance;
                        }
                        else
                        {
                            args[i] = namedSingleton;
                        }
                        continue;
                    }

                    // Если именованная регистрация не найдена, пробуем стандартное разрешение
                }

                var service = GetService(paramType, DefaultPolicy);
                args[i] = service;
            }

            return constructor.Invoke(args);
        }

        private bool AddValidation<TKey>()
        {
            return !(_containersTransient.ContainsKey(typeof(TKey))) && !(_containersSingleton.ContainsKey(typeof(TKey)));

        }

        /// <summary>
        /// {RU} Инъекция зависимостей в уже созданный объект через методы с атрибутом [InjectMethod]
        /// <para>{EN} Injects dependencies into already created object through methods with [InjectMethod] attribute</para>
        /// </summary>
        public void InjectDependencies<T>(T instance)
        {
            var type = instance.GetType();

            // Ищем методы с атрибутом [InjectMethod]
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.IsDefined(typeof(InjectMethodAttribute), false));

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                object[] args = new object[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    args[i] = GetService(parameters[i].ParameterType);
                }

                method.Invoke(instance, args);
            }
        }

        private void Validate()
        {
        }
        #endregion

        public void Clear()
        {
            _containersTransient.Clear();
            _containersSingleton.Clear();
            _namedTransient.Clear();
            _namedSingleton.Clear();
        }
    }
}
