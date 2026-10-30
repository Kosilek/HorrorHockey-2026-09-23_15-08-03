using Domain.Data;
using Domain.Game.Services;
using FM.General.Core.Interfaces.Contracts;
using Shared.Core.Contracts;
using Shared.Core.Contracts.Domain;

namespace Domain.Service
{
    public class RegistrationDIDomain : IRegisterInDI
    {
        
        public void AddRegistrations(IRegistrationContext context)
        {
            context.RegisterSingleton<INavigationMapData, NavigationMapData>();
            context.RegisterSingleton<INavigationService, NavigationService>();
            context.RegisterSingleton<IRuntimeService, RuntimeService>();
        }
    }
}
