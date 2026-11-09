using ContractsServer.Data;
using ContractsServer.Data.Navigation;
using ContractsServer.Service.Navigation;
using Shared.Core.Contracts.Domain;
using System.Collections.Generic;
using UnityEngine;

namespace Domain.Data
{
    public sealed class NavigationMapData : INavigationMapData
    {
        private readonly List<NavigationMapObject> _objects = new();

        public IReadOnlyList<NavigationMapObject> Objects => _objects;

        public bool Initialize(string json)
        {
            _objects.Clear();
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("{EN} NavigationMapData: json is empty.");
                return false;
            }

            List<NavigationObstacle> obstacles = FlatMapReader.Read(json);
            if (_objects.Capacity < obstacles.Count)
                _objects.Capacity = obstacles.Count;

            for (int i = 0; i < obstacles.Count; i++)
            {
                NavigationObstacle source = obstacles[i];
                var contour = new Vector2[source.Vertices.Length];
                for (int v = 0; v < contour.Length; v++)
                    contour[v] = new Vector2(source.Vertices[v].X, source.Vertices[v].Y);

                _objects.Add(new NavigationMapObject(source.Name, contour));
            }

            if (_objects.Count == 0)
            {
                Debug.LogWarning("{EN} NavigationMapData: json has no closed contours.");
                return false;
            }

            return true;
        }
    }
}
