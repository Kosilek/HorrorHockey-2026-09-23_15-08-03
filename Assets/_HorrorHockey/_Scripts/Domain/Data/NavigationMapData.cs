using ContractsServer.Data;
using Shared.Core.Contracts.Domain;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Domain.Data
{
    public sealed class NavigationMapData : INavigationMapData
    {
        private readonly List<NavigationMapObject> _objects = new();
        private int[] _linkA = Array.Empty<int>();
        private int[] _linkB = Array.Empty<int>();

        public IReadOnlyList<NavigationMapObject> Objects => _objects;

        public bool Initialize(string json)
        {
            _objects.Clear();
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("{EN} NavigationMapData: json is empty.");
                return false;
            }

            FlatMapFileDto file = JsonUtility.FromJson<FlatMapFileDto>(json);
            if (file?.objects == null || file.objects.Length == 0)
            {
                Debug.LogWarning("{EN} NavigationMapData: json has no objects.");
                return false;
            }

            if (_objects.Capacity < file.objects.Length)
                _objects.Capacity = file.objects.Length;

            for (int i = 0; i < file.objects.Length; i++)
            {
                FlatMapObjectDto source = file.objects[i];
                if (source?.vertices == null || source.vertices.Length == 0)
                    continue;

                Vector2[] contour = BuildContour(source.vertices, source.outline);
                if (contour == null)
                {
                    Debug.LogWarning("{EN} NavigationMapData: contour failed for " + source.name);
                    continue;
                }

                _objects.Add(new NavigationMapObject(source.name, contour));
            }

            if (_objects.Count == 0)
            {
                Debug.LogWarning("{EN} NavigationMapData: objects have no vertices.");
                return false;
            }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log("{EN} NavigationMapData loaded. Objects: " + _objects.Count);
#endif
            return true;
        }

        private Vector2[] BuildContour(FlatMapPointDto[] points, int[] outline)
        {
            int count = points.Length;
            if (count < 3 || outline == null || outline.Length < 4)
                return null;

            if (_linkA.Length < count)
            {
                _linkA = new int[count];
                _linkB = new int[count];
            }

            for (int i = 0; i < count; i++)
            {
                _linkA[i] = -1;
                _linkB[i] = -1;
            }

            int pairs = outline.Length / 2;
            for (int i = 0; i < pairs; i++)
            {
                int a = outline[i * 2];
                int b = outline[i * 2 + 1];
                if ((uint)a >= (uint)count || (uint)b >= (uint)count || a == b)
                    continue;

                if (!TryLink(a, b) || !TryLink(b, a))
                    return null;
            }

            int start = -1;
            for (int i = 0; i < count; i++)
            {
                if (_linkA[i] >= 0)
                {
                    start = i;
                    break;
                }
            }

            if (start < 0)
                return null;

            var contour = new Vector2[count];
            int previous = -1;
            int current = start;
            int written = 0;
            do
            {
                if (written >= count)
                    return null;

                FlatMapPointDto point = points[current];
                contour[written++] = new Vector2(point.x, point.z);

                int next = _linkA[current] != previous ? _linkA[current] : _linkB[current];
                if (next < 0)
                    return null;

                previous = current;
                current = next;
            }
            while (current != start);

            if (written < 3)
                return null;

            if (written == count)
                return contour;

            var trimmed = new Vector2[written];
            Array.Copy(contour, trimmed, written);
            return trimmed;
        }

        private bool TryLink(int from, int to)
        {
            int first = _linkA[from];
            if (first < 0)
            {
                _linkA[from] = to;
                return true;
            }

            if (first == to)
                return true;

            int second = _linkB[from];
            if (second < 0)
            {
                _linkB[from] = to;
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