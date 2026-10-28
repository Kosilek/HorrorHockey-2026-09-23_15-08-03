using System;
using System.Threading.Tasks;

namespace _FM.Logger
{
    /// <summary>
    /// {RU} Интерфейс логгера для записи сообщений разных уровней
    /// <para>{EN} Logger interface for writing messages of different levels</para>
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// {RU} Записывает лог уровня Trace (максимальная детализация) для диагностики
        /// <para>{EN} Writes a Trace level log (most detailed) for diagnostics</para>
        /// </summary>
        /// <param name="message">{RU} Текст сообщения<para>{EN} Message text</para></param>
        /// <param name="details">{RU} Дополнительные данные (будут сериализованы в JSON)<para>{EN} Additional data (will be serialized to JSON)</para></param>
        /// <param name="exception">{RU} Исключение, если есть<para>{EN} Exception, if any</para></param>
        void LogTrace(string message, object details = null, Exception exception = null);

        /// <summary>
        /// {RU} Записывает лог для разработки и тестирования. Временный метод для логирования во время разработки, который можно легко найти и удалить после тестирования
        /// <para>{EN} Writes a log for development and testing. Temporary method for logging during development that can be easily found and removed after testing</para>
        /// </summary>
        /// <param name="message">{RU} Текст сообщения<para>{EN} Message text</para></param>
        /// <param name="details">{RU} Дополнительные данные (будут сериализованы в JSON)<para>{EN} Additional data (will be serialized to JSON)</para></param>
        /// <param name="exception">{RU} Исключение, если есть<para>{EN} Exception, if any</para></param>
        void LogDevelop(string message, object details = null, Exception exception = null);

        /// <summary>
        /// {RU} Записывает информационный лог для отслеживания общих событий приложения
        /// <para>{EN} Writes an informational log to track general application events</para>
        /// </summary>
        /// <param name="message">{RU} Текст сообщения<para>{EN} Message text</para></param>
        /// <param name="details">{RU} Дополнительные данные (будут сериализованы в JSON)<para>{EN} Additional data (will be serialized to JSON)</para></param>
        /// <param name="exception">{RU} Исключение, если есть<para>{EN} Exception, if any</para></param>
        void LogInfo(string message, object details = null, Exception exception = null);

        /// <summary>
        /// {RU} Записывает предупреждение о потенциальных проблемах
        /// <para>{EN} Writes a warning about potential issues</para>
        /// </summary>
        /// <param name="message">{RU} Текст сообщения<para>{EN} Message text</para></param>
        /// <param name="details">{RU} Дополнительные данные (будут сериализованы в JSON)<para>{EN} Additional data (will be serialized to JSON)</para></param>
        /// <param name="exception">{RU} Исключение, если есть<para>{EN} Exception, if any</para></param>
        void LogWarning(string message, object details = null, Exception exception = null);

        /// <summary>
        /// {RU} Записывает ошибку. Автоматически добавляет стек вызовов если не передано исключение
        /// <para>{EN} Writes an error. Automatically adds stack trace if no exception is provided</para>
        /// </summary>
        /// <param name="message">{RU} Текст сообщения<para>{EN} Message text</para></param>
        /// <param name="details">{RU} Дополнительные данные (будут сериализованы в JSON)<para>{EN} Additional data (will be serialized to JSON)</para></param>
        /// <param name="exception">{RU} Исключение, если есть<para>{EN} Exception, if any</para></param>
        void LogError(string message, object details = null, Exception exception = null);

        /// <summary>
        /// {RU} Записывает критическую ошибку, требующую немедленного внимания. Автоматически добавляет стек вызовов
        /// <para>{EN} Writes a critical error requiring immediate attention. Automatically adds stack trace</para>
        /// </summary>
        /// <param name="message">{RU} Текст сообщения<para>{EN} Message text</para></param>
        /// <param name="details">{RU} Дополнительные данные (будут сериализованы в JSON)<para>{EN} Additional data (will be serialized to JSON)</para></param>
        /// <param name="exception">{RU} Исключение, если есть<para>{EN} Exception, if any</para></param>
        void LogCritical(string message, object details = null, Exception exception = null);

        /// <summary>
        /// {RU} Ожидает завершения записи всех логов из очереди. Используйте перед критическими операциями
        /// <para>{EN} Waits for all logs in the queue to be written. Use before critical operations</para>
        /// </summary>
        /// <returns>{RU} Задача, представляющая асинхронную операцию<para>{EN} Task representing the asynchronous operation</para></returns>
        Task FlushAsync();

        /// <summary>
        /// {RU} Освобождает ресурсы и корректно завершает работу логгера. Должен вызываться при закрытии приложения
        /// <para>{EN} Releases resources and properly shuts down the logger. Should be called when closing the application</para>
        /// </summary>
        void Dispose();

        /// <summary>
        /// {RU} Вызывается при захвате лога. Dev Console подписывается для отображения. Фильтрация по Source (Unity/Logger) и Level
        /// <para>{EN} Fired when a log is captured. Dev Console subscribes for display. Filter by Source (Unity/Logger) and Level</para>
        /// </summary>
        event Action<LogCaptureEntry> LogCaptured;
    }
}
