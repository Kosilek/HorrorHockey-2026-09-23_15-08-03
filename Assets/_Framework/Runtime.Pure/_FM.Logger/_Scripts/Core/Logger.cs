using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace _FM.Logger
{
    /// <summary>
    /// {RU} Асинхронный логгер с поддержкой записи в файл и вывода в Unity Console
    /// <para>{EN} Asynchronous logger with support for file writing and Unity Console output</para>
    /// </summary>
    public class Logger : ILogger
    {
        private readonly SemaphoreSlim _fileSemaphore = new(1, 1);
        private readonly ConcurrentQueue<LogEntry> _logQueue = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private readonly string _logFilePath;
        private readonly string _logDirectory;
        private readonly DeviceInfo _deviceInfo;
        private readonly string _sessionId;
        private bool _isInitialized;
        private readonly Task _logProcessingTask;
        private StreamWriter _fileWriter;

        private const int _maxLogFiles = 7;
        private const int _logFlushIntervalMs = 1000;
        private const string _configResourcePath = "Data/LoggerConfig";

        private readonly LogType _currentLogTypeUnity;
        private readonly LogType _currentLogTypeFile;
        private readonly LogLevel _currentLogLevelUnity;
        private readonly LogLevel _currentLogLevelFile;

        private struct LogEntry
        {
            public string Message;
            public LogLevel Level;
            public LogType Type;
            public object Details;
            public Exception Exception;
            public string CallerStackTrace;
        }

        private bool _canWriteToFile;
        private bool _isCapturingUnityLogs;
        private bool _isProcessingUnityLog;

        /// <inheritdoc />
        public event Action<LogCaptureEntry> LogCaptured;

        public Logger()
        {
            _sessionId = Guid.NewGuid().ToString();

            try
            {
                _deviceInfo = DeviceInfoCollector.GetCurrentDeviceInfo();
            }
            catch (Exception)
            {
                _deviceInfo = new DeviceInfo();
            }

            var config = Resources.Load<Config>(_configResourcePath);
            if (config == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[Logger] Config not found at Resources/{_configResourcePath}, using defaults");
#endif
            }

            _currentLogTypeUnity = config != null ? config.MinimalLogTypeUnity : default;
            _currentLogTypeFile = config != null ? config.MinimalLogTypeFile : default;
            _currentLogLevelUnity = config != null ? config.MinimalLogLevelUnity : default;
            _currentLogLevelFile = config != null ? config.MinimalLogLevelFile : default;

            try
            {
#if UNITY_EDITOR
                string fileName = $"game_log_{DateTime.Now:yyyy-MM-dd}_pid{System.Diagnostics.Process.GetCurrentProcess().Id}.json";
#else
                string fileName = $"game_log_{DateTime.Now:yyyy-MM-dd}.json";
#endif
                _logDirectory = Path.Combine(Application.persistentDataPath, "Logs");
                _logFilePath = Path.Combine(_logDirectory, fileName);

                Directory.CreateDirectory(_logDirectory);
                CleanupOldLogFiles();
                _canWriteToFile = true;
            }
            catch (Exception ex)
            {
                _canWriteToFile = false;
                Debug.LogWarning($"[Logger] File logging disabled — failed to initialize: {ex.Message}");
            }

            _logProcessingTask = Task.Run(() => ProcessLogQueueAsync(_cancellationTokenSource.Token));
            _isInitialized = true;
        }

        /// <summary>
        /// {RU} Включает перехват всех Unity Debug.Log/LogWarning/LogError и запись их в файл
        /// <para>{EN} Enables capturing all Unity Debug.Log/LogWarning/LogError and writing them to file</para>
        /// </summary>
        public void EnableUnityLogCapture()
        {
            if (_isCapturingUnityLogs)
                return;

            _isCapturingUnityLogs = true;
            Application.logMessageReceived += OnUnityLogReceived;
        }

        /// <summary>
        /// {RU} Отключает перехват Unity логов
        /// <para>{EN} Disables Unity log capturing</para>
        /// </summary>
        public void DisableUnityLogCapture()
        {
            if (!_isCapturingUnityLogs)
                return;

            _isCapturingUnityLogs = false;
            Application.logMessageReceived -= OnUnityLogReceived;
        }

        private void OnUnityLogReceived(string condition, string stackTrace, UnityEngine.LogType unityLogType)
        {
            if (_isProcessingUnityLog)
                return;

            _isProcessingUnityLog = true;

            try
            {
                // Skip own console output ([HH:mm:ss.fff] [LVL] ...) to avoid recursion
                if (string.IsNullOrEmpty(condition) || (condition.StartsWith("[") && condition.Contains("]")))
                    return;

                var (level, type) = ConvertUnityLogType(unityLogType);

                LogCaptured?.Invoke(new LogCaptureEntry(LogCaptureSource.Unity, condition, stackTrace, level, type, unityLogType));

                if (!_canWriteToFile || level < _currentLogLevelFile || type < _currentLogTypeFile)
                    return;

                _logQueue.Enqueue(new LogEntry
                {
                    Message = $"[Unity] {condition}",
                    Level = level,
                    Type = type,
                    Details = string.IsNullOrEmpty(stackTrace) ? null : stackTrace,
                    Exception = null
                });
            }
            finally
            {
                _isProcessingUnityLog = false;
            }
        }

        private static (LogLevel level, LogType type) ConvertUnityLogType(UnityEngine.LogType unityLogType)
        {
            return unityLogType switch
            {
                UnityEngine.LogType.Error => (LogLevel.Error, LogType.Error),
                UnityEngine.LogType.Assert => (LogLevel.Error, LogType.Error),
                UnityEngine.LogType.Warning => (LogLevel.Warning, LogType.Warning),
                UnityEngine.LogType.Log => (LogLevel.Info, LogType.Info),
                UnityEngine.LogType.Exception => (LogLevel.Critical, LogType.Error),
                _ => (LogLevel.Info, LogType.Info)
            };
        }

        private void CleanupOldLogFiles()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "game_log_*.json");
                if (logFiles.Length <= _maxLogFiles)
                    return;

                var sortedFiles = logFiles
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .Skip(_maxLogFiles)
                    .ToList();

                foreach (var fileInfo in sortedFiles)
                {
                    try
                    {
                        fileInfo.Delete();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Logger] Failed to delete old log file {fileInfo.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Logger] Failed to cleanup old log files: {ex.Message}");
            }
        }

        #region Extensions Action

        /// <inheritdoc />
        public void LogTrace(string message, object details = null, Exception exception = null)
        {
            Log(message, LogLevel.Trace, LogType.Info, details, exception);
        }

        /// <inheritdoc />
        public void LogDevelop(string message, object details = null, Exception exception = null)
        {
            Log(message, LogLevel.Develop, LogType.Info, details, exception);
        }

        /// <inheritdoc />
        public void LogInfo(string message, object details = null, Exception exception = null)
        {
            Log(message, LogLevel.Info, LogType.Info, details, exception);
        }

        /// <inheritdoc />
        public void LogWarning(string message, object details = null, Exception exception = null)
        {
            if (exception == null && details == null)
            {
                details = Environment.StackTrace;
            }
            Log(message, LogLevel.Warning, LogType.Warning, details, exception);
        }

        /// <inheritdoc />
        public void LogError(string message, object details = null, Exception exception = null)
        {
            string callerStack = null;
            if (exception == null)
            {
                exception = new Exception(message);
                callerStack = GetCallerStackTrace(skipFrames: 2);
                if (details == null)
                    details = callerStack;
            }
            else if (string.IsNullOrEmpty(exception.StackTrace))
            {
                callerStack = GetCallerStackTrace(skipFrames: 2);
            }

            Log(message, LogLevel.Error, LogType.Error, details, exception, callerStack);
        }

        /// <inheritdoc />
        public void LogCritical(string message, object details = null, Exception exception = null)
        {
            string callerStack = null;
            if (exception == null)
            {
                exception = new Exception(message);
                callerStack = GetCallerStackTrace(skipFrames: 2);
                if (details == null)
                    details = callerStack;
            }
            else if (string.IsNullOrEmpty(exception.StackTrace))
            {
                callerStack = GetCallerStackTrace(skipFrames: 2);
            }

            Log(message, LogLevel.Critical, LogType.Error, details, exception, callerStack);
        }

        private static string GetStackTrace(Exception exception)
        {
            var stackTrace = new System.Diagnostics.StackTrace(exception, true);
            return stackTrace.ToString();
        }

        private static string GetCallerStackTrace(int skipFrames)
        {
            var st = new System.Diagnostics.StackTrace(skipFrames, true);
            return st.ToString();
        }

        #endregion

        private void Log(string message, LogLevel level, LogType type, object details = null, Exception exception = null, string callerStackTrace = null)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[Logger] is not initialized.");
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (level >= _currentLogLevelUnity && type >= _currentLogTypeUnity)
            {
                LogToConsole(message, level, type);
            }
#endif

            var stackTrace = exception != null ? GetStackTrace(exception) : (callerStackTrace ?? details?.ToString());
            LogCaptured?.Invoke(new LogCaptureEntry(LogCaptureSource.Logger, message, stackTrace, level, type));

            if (!_canWriteToFile || level < _currentLogLevelFile || type < _currentLogTypeFile)
                return;

            _logQueue.Enqueue(new LogEntry
            {
                Message = message,
                Level = level,
                Type = type,
                Details = details,
                Exception = exception,
                CallerStackTrace = callerStackTrace
            });
        }

        private async Task ProcessLogQueueAsync(CancellationToken cancellationToken)
        {
            var lastFlushTime = DateTime.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    while (_logQueue.TryDequeue(out LogEntry entry))
                    {
                        await LogToFileAsync(entry);
                    }

                    if ((DateTime.UtcNow - lastFlushTime).TotalMilliseconds >= _logFlushIntervalMs)
                    {
                        await FlushFileWriterAsync();
                        lastFlushTime = DateTime.UtcNow;
                    }

                    await Task.Delay(50, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Logger] Error in log processing loop: {ex.Message}");
                }
            }

            while (_logQueue.TryDequeue(out LogEntry entry))
            {
                try
                {
                    await LogToFileAsync(entry);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Logger] Error processing final log entry: {ex.Message}");
                }
            }

            await FlushFileWriterAsync();
        }

        private void LogToConsole(string message, LogLevel level, LogType type)
        {
            string formattedMessage = FormatConsoleMessage(message, level);

            switch (type)
            {
                case LogType.Info:
                    Debug.Log(formattedMessage);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(formattedMessage);
                    break;
                case LogType.Error:
                    Debug.LogError(formattedMessage);
                    break;
            }
        }

        private async Task LogToFileAsync(LogEntry entry)
        {
            if (!_canWriteToFile)
                return;

            await _fileSemaphore.WaitAsync();

            try
            {
                if (_fileWriter == null)
                {
                    _fileWriter = new StreamWriter(_logFilePath, append: true)
                    {
                        AutoFlush = false
                    };
                }

                var errorReport = GenerateErrorReport(entry.Message, entry.Level, entry.Type, entry.Details, entry.Exception, entry.CallerStackTrace);
                string json = JsonUtility.ToJson(errorReport, true);
                await _fileWriter.WriteLineAsync(json);
            }
            catch (Exception ex)
            {
                _canWriteToFile = false;
                Debug.LogWarning($"[Logger] File logging disabled — write error: {ex.Message}");
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        private async Task FlushFileWriterAsync()
        {
            if (_fileWriter == null)
                return;

            await _fileSemaphore.WaitAsync();
            try
            {
                await _fileWriter.FlushAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Logger] Failed to flush log file: {ex.Message}");
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        private static string FormatConsoleMessage(string message, LogLevel level)
        {
            string levelTag = GetLevelTag(level);
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            return $"[{timestamp}] [{levelTag}] {message}";
        }

        private static string GetLevelTag(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => "TRC",
                LogLevel.Debug => "DBG",
                LogLevel.Develop => "DEV",
                LogLevel.Info => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Critical => "CRT",
                _ => "LOG"
            };
        }

        private ErrorReport GenerateErrorReport(string message, LogLevel level, LogType type, object details = null, Exception exception = null, string callerStackTrace = null)
        {
            string localStackTrace = "";

            if (level >= LogLevel.Error)
            {
                localStackTrace = exception?.StackTrace;
                if (string.IsNullOrWhiteSpace(localStackTrace) && !string.IsNullOrWhiteSpace(callerStackTrace))
                    localStackTrace = callerStackTrace;
                if (string.IsNullOrWhiteSpace(localStackTrace))
                    localStackTrace = Environment.StackTrace;
            }

            return new ErrorReport(
                message: message,
                level: level.ToString(),
                timestamp: DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                type: type.ToString(),
                deviceInfo: _deviceInfo,
                details: details,
                stackTrace: localStackTrace,
                sessionId: _sessionId
            );
        }

        /// <inheritdoc />
        public async Task FlushAsync()
        {
            if (!_isInitialized)
                return;

            int maxWaitMs = 5000;
            int waitedMs = 0;
            while (!_logQueue.IsEmpty && waitedMs < maxWaitMs)
            {
                await Task.Delay(50);
                waitedMs += 50;
            }

            await FlushFileWriterAsync();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_isInitialized)
                return;

            try
            {
                DisableUnityLogCapture();

                _cancellationTokenSource.Cancel();
                _logProcessingTask?.Wait(TimeSpan.FromSeconds(5));

                _fileWriter?.Flush();
                _fileWriter?.Dispose();
                _fileWriter = null;

                _fileSemaphore?.Dispose();
                _cancellationTokenSource?.Dispose();

                _isInitialized = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Logger] Error during disposal: {ex.Message}");
            }
        }
    }
}
