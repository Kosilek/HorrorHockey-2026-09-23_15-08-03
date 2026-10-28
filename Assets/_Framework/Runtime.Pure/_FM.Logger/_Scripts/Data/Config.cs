using UnityEngine;

namespace _FM.Logger
{
    /// <summary>
    /// {RU} Конфигурация логгера с настройками уровней логирования для разных назначений
    /// <para>{EN} Logger configuration with log level settings for different destinations</para>
    /// </summary>
    [CreateAssetMenu(fileName = "LoggerConfig", menuName = "_FM/Logger/LoggerConfig")]
    public class Config : ScriptableObject
    {
        [Header("Unity Console Logging")]
        [SerializeField] private LogType minimalLogTypeUnity;
        [SerializeField] private LogLevel minimalLogLevelUnity;

        [Header("File Logging")]
        [SerializeField] private LogType minimalLogTypeFile;
        [SerializeField] private LogLevel minimalLogLevelFile;

        /// <summary>
        /// {RU} Минимальный тип лога для вывода в Unity Console (видно в редакторе и development сборках)
        /// <para>{EN} Minimum log type for Unity Console output (visible in editor and development builds)</para>
        /// </summary>
        public LogType MinimalLogTypeUnity => minimalLogTypeUnity;

        /// <summary>
        /// {RU} Минимальный уровень лога для вывода в Unity Console
        /// <para>{EN} Minimum log level for Unity Console output</para>
        /// </summary>
        public LogLevel MinimalLogLevelUnity => minimalLogLevelUnity;

        /// <summary>
        /// {RU} Минимальный тип лога для записи в файл
        /// <para>{EN} Minimum log type for file writing</para>
        /// </summary>
        public LogType MinimalLogTypeFile => minimalLogTypeFile;

        /// <summary>
        /// {RU} Минимальный уровень лога для записи в файл
        /// <para>{EN} Minimum log level for file writing</para>
        /// </summary>
        public LogLevel MinimalLogLevelFile => minimalLogLevelFile;
    }
}
