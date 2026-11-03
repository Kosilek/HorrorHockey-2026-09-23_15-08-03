using FM.General.Core.Interfaces.Contracts;
using Mono.Cecil;
using Shared.Data.Provider;
using UnityEngine;

namespace UnityLife.Service
{
    public class RegistrationDIUnityLife : IRegisterInDI
    {
        private string PathProvider
        {
            get
            {
                return "ProviderService";
            }
        }

        public void AddRegistrations(IRegistrationContext context)
        {
            var providerService = Resources.Load<ProviderService>(PathProvider);
            if (providerService == null)
            {
                Debug.LogError($"ProviderService not found at path: {PathProvider}");

            }
            else
            {
                providerService.Initialize();
                context.RegisterSingleton(providerService);
            }
        }
    }
}
