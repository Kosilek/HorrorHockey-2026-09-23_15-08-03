using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace _FM.DI
{
    /// <summary>
    /// {RU} Сервис для инъекции зависимостей в Unity компоненты через атрибут [Inject]
    /// <para>{EN} Service for injecting dependencies into Unity components using [Inject] attribute</para>
    /// </summary>
    public class UnityInjectService
    {
        public static UnityInjectService Instance { get; set; }

        private readonly List<IResolver> _resolvers = new();

        /// <summary>
        /// {RU} Создает экземпляр сервиса инъекции
        /// <para>{EN} Creates an instance of injection service</para>
        /// </summary>
        /// <param name="resolver">{RU} Резолвер для получения зависимостей<para>{EN} Resolver to get dependencies</para></param>
        public UnityInjectService(params IResolver[] resolvers)
        {
            foreach(var resolver in resolvers)
            {
                _resolvers.Add(resolver ?? throw new ArgumentNullException(nameof(resolvers)));
            }
        }

        /// <summary>
        /// {RU} Инжектит зависимости во все поля и свойства объекта, помеченные атрибутом [Inject]
        /// <para>{EN} Injects dependencies into all fields and properties marked with [Inject] attribute</para>
        /// </summary>
        /// <typeparam name="T">{RU} Тип объекта<para>{EN} Object type</para></typeparam>
        /// <param name="target">{RU} Целевой объект для инъекции<para>{EN} Target object for injection</para></param>
        public void Inject<T>(T target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var type = target.GetType();

            // ВСЕГДА логируем для EntityEnemy, чтобы увидеть все вызовы
            bool isEntityEnemy = type.Name.Contains("EntityEnemy");


            // Инжектим в поля
            // ВАЖНО: GetFields по умолчанию не получает поля из базовых классов, нужно пройти по иерархии
            var fields = GetAllFields(type);

            int fieldsProcessed = 0;
            foreach (var field in fields)
            {
                if (field.IsDefined(typeof(InjectAttribute), true))
                {
                    fieldsProcessed++;
                    var fieldValueBefore = field.GetValue(target);

                    // Сбрасываем поле перед инжекцией для корректной работы с переиспользованием объектов из пула
                    field.SetValue(target, null);

                    var dependency = ResolveDependency(field.FieldType);
                    if (dependency != null)
                    {
                        field.SetValue(target, dependency);
                    }
                }
            }

            // Инжектим в свойства
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.IsDefined(typeof(InjectAttribute), true) && property.CanWrite)
                {
                    // Сбрасываем свойство перед инжекцией
                    property.SetValue(target, null);

                    var dependency = ResolveDependency(property.PropertyType);
                    if (dependency != null)
                    {
                        property.SetValue(target, dependency);
                    }
                }
            }
        }

        private List<FieldInfo> GetAllFields(Type type)
        {
            var fields = new List<FieldInfo>();
            var currentType = type;

            while (currentType != null && currentType != typeof(object))
            {
                var declaredFields = currentType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                fields.AddRange(declaredFields);
                currentType = currentType.BaseType;
            }

            return fields;
        }

        private object ResolveDependency(Type dependencyType)
        {
            // Явно указываем перегрузку Resolve<T>(ResolutionPolicy?) для избежания AmbiguousMatchException
            // Ищем метод с одним параметром типа ResolutionPolicy? (не string)
            var resolveMethods = typeof(IResolver).GetMethods()
                .Where(m => m.Name == nameof(IResolver.Resolve))
                .Where(m => m.GetParameters().Length == 1)
                .Where(m => m.GetParameters()[0].ParameterType == typeof(ResolutionPolicy?))
                .ToList();

            if (resolveMethods.Count == 0)
            {
                throw new InvalidOperationException($"Could not find Resolve<T>(ResolutionPolicy?) method in IResolver interface");
            }

            var resolveMethod = resolveMethods[0];
            var genericMethod = resolveMethod.MakeGenericMethod(dependencyType);

            for (int i = 0; i < _resolvers.Count; i++)
            {
                try
                {
                    // Используем TryGlobalFallback для резолверов, которые могут иметь FallbackContainer
                    // Это позволяет MatchContext использовать RootContext как fallback
                    var resolve = genericMethod.Invoke(_resolvers[i], new object[] { ResolutionPolicy.TryGlobalFallback });
                    if (resolve != null)
                    {
                        return resolve;
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[UnityInjectService] Failed to resolve dependency '{dependencyType.Name}' with resolver [{i}] ({_resolvers[i].GetType().Name}): {ex.Message}\nStackTrace: {ex.StackTrace}");
                    continue;
                }
            }
            return null;
        }
    }
}
