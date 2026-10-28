using System;
using UnityEngine;

namespace ContractsServer.Data
{
    public sealed class NavigationMapObject
    {
        private const float BoundsPad = 0.01f;

        public string Name { get; }
        public Vector2[] Vertices { get; }
        public Vector2 Min { get; }
        public Vector2 Max { get; }

        public NavigationMapObject(string name, Vector2[] vertices)
        {
            Name = name ?? string.Empty;
            Vertices = vertices ?? Array.Empty<Vector2>();
            BuildBounds(Vertices, out Vector2 min, out Vector2 max);
            Min = min;
            Max = max;
        }

        private static void BuildBounds(Vector2[] vertices, out Vector2 min, out Vector2 max)
        {
            int length = vertices.Length;
            if (length == 0)
            {
                min = default;
                max = default;
                return;
            }

            float minX = vertices[0].x;
            float minY = vertices[0].y;
            float maxX = minX;
            float maxY = minY;
            for (int i = 1; i < length; i++)
            {
                Vector2 vertex = vertices[i];
                if (vertex.x < minX)
                    minX = vertex.x;
                else if (vertex.x > maxX)
                    maxX = vertex.x;

                if (vertex.y < minY)
                    minY = vertex.y;
                else if (vertex.y > maxY)
                    maxY = vertex.y;
            }

            min = new Vector2(minX - BoundsPad, minY - BoundsPad);
            max = new Vector2(maxX + BoundsPad, maxY + BoundsPad);
        }
    }
}