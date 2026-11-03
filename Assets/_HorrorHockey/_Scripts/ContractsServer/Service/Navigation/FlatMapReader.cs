using ContractsServer.Data.Navigation;
using System;
using System.Collections.Generic;
#if SERVER
using Newtonsoft.Json;
#else
using UnityEngine;
#endif
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace ContractsServer.Service.Navigation
{
    /// <summary>
    /// <para>{EN} Reads FlatMap.json into closed obstacle contours. Same format as the client NavigationMapData.</para>
    /// {RU} Читает FlatMap.json в замкнутые контуры препятствий. Формат тот же, что у клиентской NavigationMapData.
    /// </summary>
    public static class FlatMapReader
    {
        public static List<NavigationObstacle> Read(string json)
        {
            var result = new List<NavigationObstacle>();
            if (string.IsNullOrEmpty(json))
                return result;

#if SERVER
            FlatMapFileDto file = JsonConvert.DeserializeObject<FlatMapFileDto>(json);
#else
            FlatMapFileDto file = JsonUtility.FromJson<FlatMapFileDto>(json);
#endif
            if (file?.objects == null)
                return result;

            int[] linkA = Array.Empty<int>();
            int[] linkB = Array.Empty<int>();
            for (int i = 0; i < file.objects.Length; i++)
            {
                FlatMapObjectDto source = file.objects[i];
                if (source?.vertices == null || source.vertices.Length == 0)
                    continue;

                if (linkA.Length < source.vertices.Length)
                {
                    linkA = new int[source.vertices.Length];
                    linkB = new int[source.vertices.Length];
                }

                AddContours(result, source.name, source.vertices, source.outline, linkA, linkB);
            }

            return result;
        }

        /// <summary>
        /// <para>{EN} One object can hold several loops, e.g. the outer and inner side of a fence. Each closed loop becomes its own obstacle.</para>
        /// {RU} В одном объекте может быть несколько петель, например внешняя и внутренняя сторона забора. Каждая замкнутая петля — отдельное препятствие.
        /// </summary>
        private static void AddContours(List<NavigationObstacle> result, string name, FlatMapPointDto[] points, int[] outline, int[] linkA, int[] linkB)
        {
            int count = points.Length;
            if (count < 3 || outline == null || outline.Length < 4)
                return;

            for (int i = 0; i < count; i++)
            {
                linkA[i] = -1;
                linkB[i] = -1;
            }

            int pairs = outline.Length / 2;
            for (int i = 0; i < pairs; i++)
            {
                int a = outline[i * 2];
                int b = outline[i * 2 + 1];
                if ((uint)a >= (uint)count || (uint)b >= (uint)count || a == b)
                    continue;

                if (!TryLink(linkA, linkB, a, b) || !TryLink(linkA, linkB, b, a))
                    return;
            }

            var visited = new bool[count];
            var loop = new List<FMVector2>(count);
            for (int start = 0; start < count; start++)
            {
                if (visited[start] || linkA[start] < 0)
                    continue;

                loop.Clear();
                int previous = -1;
                int current = start;
                bool closed = false;
                while (!visited[current])
                {
                    visited[current] = true;
                    FlatMapPointDto point = points[current];
                    loop.Add(new FMVector2(point.x, point.z));

                    int next = linkA[current] != previous ? linkA[current] : linkB[current];
                    if (next < 0)
                        break;

                    previous = current;
                    current = next;
                    if (current == start)
                    {
                        closed = true;
                        break;
                    }
                }

                if (closed && loop.Count >= 3)
                    result.Add(new NavigationObstacle(name, loop.ToArray()));
            }
        }

        private static bool TryLink(int[] linkA, int[] linkB, int from, int to)
        {
            int first = linkA[from];
            if (first < 0)
            {
                linkA[from] = to;
                return true;
            }

            if (first == to)
                return true;

            int second = linkB[from];
            if (second < 0)
            {
                linkB[from] = to;
                return true;
            }

            return second == to;
        }

        [Serializable]
        private sealed class FlatMapFileDto
        {
            public FlatMapObjectDto[] objects = null;
        }

        [Serializable]
        private sealed class FlatMapObjectDto
        {
            public string name = null;
            public FlatMapPointDto[] vertices = null;
            public int[] outline = null;
        }

#pragma warning disable CS0649
        [Serializable]
        private struct FlatMapPointDto
        {
            public float x;
            public float z;
        }
#pragma warning restore CS0649
    }
}
