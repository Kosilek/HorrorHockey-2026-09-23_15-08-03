using ContractsServer.Data;
using System.Collections.Generic;

namespace Shared.Core.Contracts.Domain
{
    /// <summary>
    /// <para>{RU} Список объектов FlatMap. Внутри каждого объекта — контур его края по порядку.</para>
    /// <para>{EN} FlatMap object list. Each object holds its boundary contour in order.</para>
    /// </summary>
    public interface INavigationMapData
    {
        IReadOnlyList<NavigationMapObject> Objects { get; }

        /// <summary>
        /// <para>{RU} Собирает list объектов из json и сцепляет рёбра outline в контур. Повторный вызов очищает старый список.</para>
        /// <para>{EN} Builds the object list from json and chains outline edges into a contour. A repeat call clears the previous list.</para>
        /// </summary>
        bool Initialize(string json);
    }
}