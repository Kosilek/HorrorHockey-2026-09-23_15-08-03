using System;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace ContractsServer.Data.Navigation
{
    /// <summary>
    /// <para>{EN} Impassable contour of a map object. Y of a vertex is world Z.</para>
    /// {RU} Непроходимый контур объекта карты. Y вершины — мировая Z.
    /// </summary>
    public sealed class NavigationObstacle
    {
        public string Name { get; }
        public FMVector2[] Vertices { get; }

        public NavigationObstacle(string name, FMVector2[] vertices)
        {
            Name = name ?? string.Empty;
            Vertices = vertices ?? Array.Empty<FMVector2>();
        }
    }
}
