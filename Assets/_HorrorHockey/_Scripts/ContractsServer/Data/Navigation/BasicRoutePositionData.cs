using FM.Mathematics.DataTypes;
using System;

namespace ContractsServer.Data.Navigation
{
    [Serializable]
    public class BasicRoutePositionData
    {
        public BasicRoutePositionData(
            Vector2[] leftRoute,
            Vector2[] middleRoute,
            Vector2[] rightRoute)
        {
            LeftRoute = leftRoute;
            MiddleRoute = middleRoute;
            RightRoute = rightRoute;
        }
        public Vector2[] LeftRoute;
        public Vector2[] MiddleRoute;
        public Vector2[] RightRoute;
    }
}
