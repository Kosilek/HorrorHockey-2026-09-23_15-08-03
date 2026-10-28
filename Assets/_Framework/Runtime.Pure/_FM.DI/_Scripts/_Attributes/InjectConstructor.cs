using System;

namespace _FM.DI
{
    /// <summary>
    /// {RU} Атрибут для пометки конструктора, который должен использоваться DI контейнером для инъекции зависимостей
    /// <para>{EN} Attribute to mark constructor that should be used by DI container for dependency injection</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor)]
    public class InjectConstructorAttribute : Attribute
    {
        
    }
}
