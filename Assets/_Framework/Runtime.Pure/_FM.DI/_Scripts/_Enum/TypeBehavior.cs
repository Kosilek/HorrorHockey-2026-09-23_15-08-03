namespace _FM.DI
{
    /// <summary>
    /// {RU} Политика разрешения зависимостей при отсутствии зарегистрированного типа
    /// <para>{EN} Dependency resolution policy when registered type is not found</para>
    /// </summary>
    public enum ResolutionPolicy
    {
        /// <summary>
        /// {RU} Выбросить исключение если тип не найден (рекомендуется для разработки)
        /// <para>{EN} Throw exception if type is not found (recommended for development)</para>
        /// </summary>
        ThrowException,
        
        /// <summary>
        /// {RU} Вернуть null если тип не найден
        /// <para>{EN} Return null if type is not found</para>
        /// </summary>
        ReturnNull,
        
        /// <summary>
        /// {RU} Попытаться разрешить зависимость через резервный контейнер (FallbackContainer)
        /// <para>{EN} Try to resolve dependency through fallback container (FallbackContainer)</para>
        /// </summary>
        TryGlobalFallback
    }
}