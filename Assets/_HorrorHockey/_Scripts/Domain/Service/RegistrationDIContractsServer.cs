using ContractsServer.Core.Interfaces;
using ContractsServer.Data.Navigation;
using ContractsServer.Service.Navigation;
using FM.General.Core.Interfaces.Contracts;

namespace Domain.Service
{
    public class RegistrationDIContractsServer : IRegisterInDI
    {
        public void AddRegistrations(IRegistrationContext context)
        {
            context.RegisterSingleton<INavigationProviderService, NavigationProviderService>();
        }
    }
}
