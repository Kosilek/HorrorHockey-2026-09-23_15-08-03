using ContractsServer.Core.Interfaces;
using ContractsServer.Data.Navigation;
using SimpleJSON;
using System.IO;
using UnityEngine;

namespace ContractsServer.Service.Navigation
{
    public class NavigationProviderService : INavigationProviderService
    {
        private readonly BasicRouteData _basicRouteData;
        public BasicRouteData BasicRouteData => _basicRouteData;

        private string PathBaseRoute
        {
            get 
            {
#if SERVER
return null;
#else
                return Path.Combine(
                    Application.dataPath,
                    "_HorrorHockey/_Scripts/ContractsServer/Json/Navigation/BasicRouteData.json");
#endif
            }
        }

        public NavigationProviderService()
        {
            string json = File.ReadAllText(PathBaseRoute);
#if SERVER
        _basicRouteData = JsonConvert.DeserializeObject<BasicRouteData>(json);
#else
            _basicRouteData = JsonUtility.FromJson<BasicRouteData>(json);
#endif
        }
    }
}
