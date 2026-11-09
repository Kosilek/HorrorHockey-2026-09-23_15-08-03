using ContractsServer.Service.Navigation;

namespace Server.Core.Entity
{
    public partial class UnitRuntime
    {
        public UnitRuntime()
        { 
            _routeMotion = new();
            _random = new();
        }
    }
}