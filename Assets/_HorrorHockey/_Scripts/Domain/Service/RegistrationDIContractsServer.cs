using ContractsServer.Core.Interfaces;
using ContractsServer.Service.Navigation;
using ContractsServer.Service.Entity;
using FM.General.Core.Interfaces.Contracts;

namespace Domain.Service
{
    public class RegistrationDIContractsServer : IRegisterInDI
    {
        public void AddRegistrations(IRegistrationContext context)
        {
            context.RegisterSingleton<INavigationProviderService, NavigationProviderService>();
            context.RegisterSingleton<IEntityProviderService, EntityProviderService>();
        }
    }
}
