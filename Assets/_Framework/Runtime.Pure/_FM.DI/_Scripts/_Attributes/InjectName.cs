using System;

namespace _FM.DI
{
    /// <summary>
    /// {RU} Атрибут для указания имени зависимости в параметре конструктора
    /// <para>{EN} Attribute to specify dependency name in constructor parameter</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class InjectNameAttribute : Attribute
    {
        /// <summary>
        /// {RU} Имя зависимости для разрешения
        /// <para>{EN} Dependency name for resolution</para>
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// {RU} Создает новый экземпляр атрибута с указанным именем
        /// <para>{EN} Creates new attribute instance with specified name</para>
        /// </summary>
        /// <param name="name">{RU} Имя зависимости<para>{EN} Dependency name</para></param>
        public InjectNameAttribute(string name)
        {
            Name = name;
        }
    }
}






































