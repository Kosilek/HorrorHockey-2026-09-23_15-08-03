using System;

namespace _FM.DI
{
    /// <summary>
    /// {RU} Атрибут для пометки полей и свойств для автоматической инъекции зависимостей (функция в разработке)
    /// <para>{EN} Attribute to mark fields and properties for automatic dependency injection (feature in development)</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class InjectAttribute : Attribute
    {

    }
}