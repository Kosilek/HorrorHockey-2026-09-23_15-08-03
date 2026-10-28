using System;
using System.Collections.Generic;
using System.Reflection;

namespace _FM.DI
{
    public partial class DIManagerOptions
    {
        /// <summary>
        /// {RU} Сборки для проверки наличия модулей (HasAssembly)
        /// <para>{EN} Assemblies for module presence check</para>
        /// </summary>
        public Assembly[] Assemblies { get; set; }

        /// <summary>
        /// {RU} Регистрации от внешних модулей (Logger, Config). DiBootstrap заполняет через IRegisterInDI.
        /// <para>{EN} Registrations from external modules. DiBootstrap fills via IRegisterInDI.</para>
        /// </summary>
        public List<Action<IDIManager, DIManagerOptions>> ExternalRegistrations { get; } = new();

        /// <summary>
        /// {RU} Контейнер (устанавливается DIManager при Initialize). Для внутренних init.
        /// <para>{EN} Container (set by DIManager on Initialize)</para>
        /// </summary>
        public IServiceContainer Container { get; internal set; }

        /// <summary>
        /// {RU} Проверяет наличие сборки по имени
        /// <para>{EN} Checks if assembly is present by name</para>
        /// </summary>
        public bool HasAssembly(string name)
        {
            if (Assemblies == null) return false;
            foreach (var asm in Assemblies)
            {
                if (asm.GetName().Name == name) return true;
            }
            return false;
        }
    }
}