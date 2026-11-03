using ContractsServer.Core.Enum;
using ContractsServer.Core.Interfaces;
using ContractsServer.Data.Navigation;
using FM.Mathematics.DataTypes;
using Server.Core.Interfaces;

namespace Server.Service.Navigation
{
    public class NavigationService : INavigationService
    {
        private readonly BasicRoutePositionData _blueTeamRoute;
        private readonly BasicRoutePositionData _redTeamRoute;
        private readonly INavigationProviderService _navigationProviderService;


        public NavigationService(INavigationProviderService navigationProviderService)
        {
            _navigationProviderService = navigationProviderService;
            _blueTeamRoute = navigationProviderService.BlueRoutes;
            _redTeamRoute = navigationProviderService.RedRoutes;
        }

        #region Public API
        public Vector2 GetStartPosition(PlayerIndex playerIndex)
        {
            return playerIndex switch
            {
                PlayerIndex.BlueLeft => _blueTeamRoute.LeftRoute[0],
                PlayerIndex.BlueMiddle => _blueTeamRoute.MiddleRoute[0],
                PlayerIndex.BlueRight => _blueTeamRoute.RightRoute[0],
                PlayerIndex.RedLeft => _redTeamRoute.LeftRoute[0],
                PlayerIndex.RedMiddle => _redTeamRoute.MiddleRoute[0],
                PlayerIndex.RedRight => _redTeamRoute.RightRoute[0],
                _ => throw new System.ArgumentOutOfRangeException(nameof(playerIndex), playerIndex, null)
            };
        }
        #endregion

        public bool TryGetRoute(TeamSide team, RouteLane lane, out Vector2[] controls)
        {
            controls = null;
            Vector2[] source = Lane(team == TeamSide.Red ? _redTeamRoute : _blueTeamRoute, lane);
            if (source == null || source.Length == 0)
                return false;

            controls = (Vector2[])source.Clone();
            return true;
        }

        public bool TryBuildRoute(Vector2 from, Vector2 to, out Vector2[] controls)
        {
            controls = _navigationProviderService.BuildRoute(from, to);
            return controls != null && controls.Length >= 2;
        }

        public bool TryStart(int unitId, Vector2 start, Vector2[] controls)
        {
      //      var motion = new UnitRouteMotion();
      //      if (!motion.TryStart(start, controls, GameConstants.CurveStep))
                return false;

         //////   _units[unitId] = motion;
       //     return true;
        }

        public bool TryStartLane(int unitId, Vector2 start, TeamSide team, RouteLane lane)
        {
            BasicRoutePositionData routes = team == TeamSide.Red ? _redTeamRoute : _blueTeamRoute;
            Vector2[] controls = Lane(routes, lane);
            if (controls == null || controls.Length == 0)
                return false;

            return TryStart(unitId, start, controls);
        }

        public void Tick(float deltaTime)
        {
            //foreach (UnitRouteMotion motion in _units.Values)
            //{
            //    motion.Tick(deltaTime, GameConstants.MaxSpeed, GameConstants.AccelTime, GameConstants.BrakeTime);
            //}
        }

        private static Vector2[] Lane(BasicRoutePositionData routes, RouteLane lane)
        {
            if (lane == RouteLane.Middle)
                return routes.MiddleRoute;
            if (lane == RouteLane.Right)
                return routes.RightRoute;
            return routes.LeftRoute;
        }
    }
}
