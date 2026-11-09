using ContractsServer.Core.Interfaces;
using ContractsServer.Data;
using ContractsServer.Data.Navigation;
using SimpleJSON;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace ContractsServer.Service.Navigation
{
    public class NavigationProviderService : INavigationProviderService
    {
        private readonly BasicRouteData _basicRouteData;
        public BasicRouteData BasicRouteData => _basicRouteData;

        private readonly List<NavigationObstacle> _obstacles;
        public IReadOnlyList<NavigationObstacle> Obstacles => _obstacles;

        private readonly BasicRoutePositionData _blueRoutes;
        public BasicRoutePositionData BlueRoutes => _blueRoutes;

        private readonly BasicRoutePositionData _redRoutes;
        public BasicRoutePositionData RedRoutes => _redRoutes;

        private readonly RouteBypass _bypass;
        private readonly object _bypassLock = new();

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

        private string PathFlatMap
        {
            get
            {
#if SERVER
return null;
#else
                return Path.Combine(
                    Application.dataPath,
                    "_HorrorHockey/Data/FlatMap/FlatMap.json");
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

            string flatMapPath = PathFlatMap;
            if (!string.IsNullOrEmpty(flatMapPath) && File.Exists(flatMapPath))
                _obstacles = FlatMapReader.Read(File.ReadAllText(flatMapPath));
            else
                _obstacles = new List<NavigationObstacle>();

            if (_obstacles.Count == 0)
                Debug.LogWarning("{EN} NavigationProviderService: no obstacles loaded. Routes skip the bypass. " + flatMapPath);

            FMVector2[] left = Positions(_basicRouteData.LeftRoute);
            FMVector2[] middle = Positions(_basicRouteData.MiddleRoute);
            FMVector2[] right = Positions(_basicRouteData.RightRoute);

            _bypass = new RouteBypass(_obstacles, GameConstants.RouteClearance, GameConstants.CurveStep);
            _blueRoutes = new BasicRoutePositionData(
                BuildRoute(left),
                BuildRoute(middle),
                BuildRoute(right));
            _redRoutes = new BasicRoutePositionData(
                BuildRoute(Reversed(left)),
                BuildRoute(Reversed(middle)),
                BuildRoute(Reversed(right)));
        }

        public FMVector2[] BuildRoute(FMVector2[] waypoints)
        {
            // RouteBypass переиспользует внутренние списки, а матчи тикают в своих потоках.
            lock (_bypassLock)
                return _bypass.Build(waypoints);
        }

        public FMVector2[] BuildRoute(FMVector2 from, FMVector2 to)
        {
            return BuildRoute(new[] { from, to });
        }

        public bool IsWalkable(FMVector2 point)
        {
            return _bypass.IsWalkable(point);
        }

        private static FMVector2[] Positions(BasicObjectData[] route)
        {
            if (route == null)
                return System.Array.Empty<FMVector2>();

            var positions = new FMVector2[route.Length];
            for (int i = 0; i < route.Length; i++)
                positions[i] = route[i].Position;
            return positions;
        }

        private static FMVector2[] Reversed(FMVector2[] route)
        {
            var reversed = (FMVector2[])route.Clone();
            System.Array.Reverse(reversed);
            return reversed;
        }
    }
}
