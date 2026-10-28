using UnityEngine;

namespace _FM.Logger
{
    /// <summary>
    /// {RU} Источник лога для фильтрации в Dev Console
    /// <para>{EN} Log source for Dev Console filtering</para>
    /// </summary>
    public enum LogCaptureSource
    {
        Unity,
        Logger
    }

    /// <summary>
    /// {RU} Захваченный лог для передачи в Dev Console. Содержит источник (Unity/Logger), уровень и тип для фильтрации
    /// <para>{EN} Captured log for Dev Console. Contains source (Unity/Logger), level and type for filtering</para>
    /// </summary>
    public readonly struct LogCaptureEntry
    {
        public LogCaptureSource Source { get; }
        public string Message { get; }
        public string StackTrace { get; }
        public LogLevel Level { get; }
        public LogType Type { get; }
        public UnityEngine.LogType UnityLogType { get; }

        public LogCaptureEntry(LogCaptureSource source, string message, string stackTrace, LogLevel level, LogType type, UnityEngine.LogType unityLogType = default)
        {
            Source = source;
            Message = message;
            StackTrace = stackTrace;
            Level = level;
            Type = type;
            UnityLogType = unityLogType;
        }
    }

    /// <summary>
    /// {RU} Структура отчета об ошибке/логе для записи в файл
    /// <para>{EN} Error/log report structure for file writing</para>
    /// </summary>
    [System.Serializable]
    public class ErrorReport
    {
        /// <summary>
        /// {RU} Текст сообщения лога
        /// <para>{EN} Log message text</para>
        /// </summary>
        public string message;
        
        /// <summary>
        /// {RU} Уровень логирования (Trace, Debug, Info, Warning, Error, Critical)
        /// <para>{EN} Log level (Trace, Debug, Info, Warning, Error, Critical)</para>
        /// </summary>
        public string level;
        
        /// <summary>
        /// {RU} Временная метка в формате ISO 8601 UTC
        /// <para>{EN} Timestamp in ISO 8601 UTC format</para>
        /// </summary>
        public string timestamp;
        
        /// <summary>
        /// {RU} Тип лога (Info, Warning, Error)
        /// <para>{EN} Log type (Info, Warning, Error)</para>
        /// </summary>
        public string type;
        
        /// <summary>
        /// {RU} Информация об устройстве пользователя
        /// <para>{EN} User device information</para>
        /// </summary>
        public DeviceInfo deviceInfo;
        
        /// <summary>
        /// {RU} Дополнительные данные (сериализуются в JSON)
        /// <para>{EN} Additional data (serialized to JSON)</para>
        /// </summary>
        public object details;
        
        /// <summary>
        /// {RU} Стек вызовов для ошибок и критических логов
        /// <para>{EN} Stack trace for errors and critical logs</para>
        /// </summary>
        public string stackTrace;
        
        /// <summary>
        /// {RU} Уникальный идентификатор сессии игры
        /// <para>{EN} Unique game session identifier</para>
        /// </summary>
        public string sessionId;

        /// <summary>
        /// {RU} Конструктор по умолчанию для десериализации
        /// <para>{EN} Default constructor for deserialization</para>
        /// </summary>
        public ErrorReport()
        {

        }

        /// <summary>
        /// {RU} Создает новый отчет об ошибке/логе со всеми параметрами
        /// <para>{EN} Creates a new error/log report with all parameters</para>
        /// </summary>
        /// <param name="message">{RU} Текст сообщения<para>{EN} Message text</para></param>
        /// <param name="level">{RU} Уровень логирования<para>{EN} Log level</para></param>
        /// <param name="timestamp">{RU} Временная метка<para>{EN} Timestamp</para></param>
        /// <param name="type">{RU} Тип лога<para>{EN} Log type</para></param>
        /// <param name="deviceInfo">{RU} Информация об устройстве<para>{EN} Device information</para></param>
        /// <param name="details">{RU} Дополнительные данные<para>{EN} Additional data</para></param>
        /// <param name="stackTrace">{RU} Стек вызовов<para>{EN} Stack trace</para></param>
        /// <param name="sessionId">{RU} ID сессии<para>{EN} Session ID</para></param>
        public ErrorReport(string message, string level, string timestamp, string type, DeviceInfo deviceInfo, object details, string stackTrace, string sessionId)
        {
            this.message = message;
            this.level = level;
            this.timestamp = timestamp;
            this.type = type;
            this.deviceInfo = deviceInfo;
            this.details = details;
            this.stackTrace = stackTrace;
            this.sessionId = sessionId;
        }
    }
}