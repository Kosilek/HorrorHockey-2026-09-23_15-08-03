using FM.General.Core.Interfaces.Contracts;
using Shared.Core.Contracts.Domain;
using UnityEngine;

namespace Domain.Service
{
    public class RegistrationDIDomain : IRegisterInDI
    {
        public void AddRegistrations(IRegistrationContext context)
        {
            context.RegisterSingleton<INavigationService, NavigationService>();
        }
    }
}
