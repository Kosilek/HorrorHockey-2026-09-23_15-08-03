namespace _FM.Logger
{
    /// <summary>
    /// {RU} Тип лога для Unity Console
    /// <para>{EN} Log type for Unity Console</para>
    /// </summary>
    public enum LogType
    {
        /// <summary>
        /// {RU} Информационное сообщение (Debug.Log)
        /// <para>{EN} Information message (Debug.Log)</para>
        /// </summary>
        Info = 0,

        /// <summary>
        /// {RU} Предупреждение (Debug.LogWarning)
        /// <para>{EN} Warning (Debug.LogWarning)</para>
        /// </summary>
        Warning = 1,

        /// <summary>
        /// {RU} Ошибка (Debug.LogError)
        /// <para>{EN} Error (Debug.LogError)</para>
        /// </summary>
        Error = 2,

        /// <summary>
        /// {RU} Отключить вывод в Unity Console
        /// <para>{EN} Disable output to Unity Console</para>
        /// </summary>
        None = 3
    }

    /// <summary>
    /// {RU} Уровень детализации логирования
    /// <para>{EN} Log verbosity level</para>
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// {RU} Максимально детальная диагностическая информация
        /// <para>{EN} Most detailed diagnostic information</para>
        /// </summary>
        Trace = 0,

        /// <summary>
        /// {RU} Отладочная информация для разработки
        /// <para>{EN} Debug information for development</para>
        /// </summary>
        Debug = 1,

        /// <summary>
        /// {RU} Лог для разработки и тестирования (временный, для удаления после тестирования)
        /// <para>{EN} Log for development and testing (temporary, to be removed after testing)</para>
        /// </summary>
        Develop = 2,

        /// <summary>
        /// {RU} Общая информация о работе приложения
        /// <para>{EN} General application information</para>
        /// </summary>
        Info = 3,

        /// <summary>
        /// {RU} Предупреждения о потенциальных проблемах
        /// <para>{EN} Warnings about potential issues</para>
        /// </summary>
        Warning = 4,

        /// <summary>
        /// {RU} Ошибки, не прерывающие работу приложения
        /// <para>{EN} Errors that don't interrupt application execution</para>
        /// </summary>
        Error = 5,

        /// <summary>
        /// {RU} Критические ошибки, требующие немедленного внимания
        /// <para>{EN} Critical errors requiring immediate attention</para>
        /// </summary>
        Critical = 6,

        /// <summary>
        /// {RU} Отключить логирование
        /// <para>{EN} Disable logging</para>
        /// </summary>
        None = 7
    }
}
