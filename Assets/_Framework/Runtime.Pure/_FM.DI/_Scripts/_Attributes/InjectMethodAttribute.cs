using System;

namespace _FM.DI
{
    /// <summary>
    /// {RU} Атрибут для методов, в которые нужно инъектировать зависимости после создания объекта
    /// <para>{EN} Attribute for methods that need dependency injection after object creation</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class InjectMethodAttribute : Attribute
    {
    }
}

