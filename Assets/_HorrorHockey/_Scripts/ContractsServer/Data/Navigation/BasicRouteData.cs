using System;

namespace ContractsServer.Data.Navigation
{
    [Serializable]
    public class BasicRouteData
    {
        public BasicObjectData[] LeftRoute;
        public BasicObjectData[] MiddleRoute;
        public BasicObjectData[] RightRoute;
    }
}
