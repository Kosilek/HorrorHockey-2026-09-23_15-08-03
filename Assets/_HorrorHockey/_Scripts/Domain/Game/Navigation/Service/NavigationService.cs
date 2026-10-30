using Shared.Core.Contracts.Domain;
using System.IO;
using UnityEngine;

namespace Domain
{
    public class NavigationService : INavigationService
    {
        private readonly INavigationMapData _navigationMapData;

        public NavigationService(INavigationMapData navigationMapData)
        {
            _navigationMapData = navigationMapData;
            LoadDefaultMap();
        }

        public bool Initialize(string json)
        {
            return _navigationMapData.Initialize(json);
        }

        private void LoadDefaultMap()
        {
            string path = Path.Combine(Application.dataPath, "_HorrorHockey", "Data", "FlatMap", "FlatMap.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("{EN} NavigationService: flat map json was not found. " + path);
                return;
            }

            Initialize(File.ReadAllText(path));
        }
    }
}
