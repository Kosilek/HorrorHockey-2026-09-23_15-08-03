using UnityEngine;

namespace _FM.Logger
{
    /// <summary>
    /// {RU} Утилита для сбора информации об устройстве и системе
    /// <para>{EN} Utility for collecting device and system information</para>
    /// </summary>
    public static class DeviceInfoCollector
    {
        /// <summary>
        /// {RU} Собирает текущую информацию об устройстве пользователя
        /// <para>{EN} Collects current user device information</para>
        /// </summary>
        /// <returns>{RU} Объект с информацией об устройстве<para>{EN} Object with device information</para></returns>
        public static DeviceInfo GetCurrentDeviceInfo()
        {
            return new DeviceInfo
            {
                platform = Application.platform.ToString(),
                model = SystemInfo.deviceModel,
                operatingSystem = SystemInfo.operatingSystem,
                gameVersion = Application.version,
                unityVersion = Application.unityVersion,
                systemMemorySize = SystemInfo.systemMemorySize,
                deviceUniqueIdentifier = SystemInfo.deviceUniqueIdentifier
            };
        }
    }

    /// <summary>
    /// {RU} Данные об устройстве пользователя для логирования
    /// <para>{EN} User device data for logging</para>
    /// </summary>
    [System.Serializable]
    public class DeviceInfo
    {
        /// <summary>
        /// {RU} Платформа (Android, iOS, Windows и т.д.)
        /// <para>{EN} Platform (Android, iOS, Windows, etc.)</para>
        /// </summary>
        public string platform;
        
        /// <summary>
        /// {RU} Модель устройства
        /// <para>{EN} Device model</para>
        /// </summary>
        public string model;
        
        /// <summary>
        /// {RU} Операционная система с версией
        /// <para>{EN} Operating system with version</para>
        /// </summary>
        public string operatingSystem;
        
        /// <summary>
        /// {RU} Версия игры/приложения
        /// <para>{EN} Game/application version</para>
        /// </summary>
        public string gameVersion;
        
        /// <summary>
        /// {RU} Версия Unity
        /// <para>{EN} Unity version</para>
        /// </summary>
        public string unityVersion;
        
        /// <summary>
        /// {RU} Размер системной памяти в МБ
        /// <para>{EN} System memory size in MB</para>
        /// </summary>
        public int systemMemorySize;
        
        /// <summary>
        /// {RU} Уникальный идентификатор устройства
        /// <para>{EN} Device unique identifier</para>
        /// </summary>
        public string deviceUniqueIdentifier;
    }
}