using ContractsServer.Data.Navigation;

namespace ContractsServer.Core.Interfaces
{
    public interface INavigationProviderService
    {
        BasicRouteData BasicRouteData { get; }
    }
}