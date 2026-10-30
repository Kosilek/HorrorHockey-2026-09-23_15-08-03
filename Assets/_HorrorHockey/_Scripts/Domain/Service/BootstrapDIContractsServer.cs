using _FM.DI;
using ContractsServer.Core.Interfaces;
using FM.General.Core.Interfaces.Contracts;
using System.Reflection;
using UnityEngine;

namespace Domain.Service
{
    public class BootstrapDIContractsServer : IBootstrap
    {
        public void Initialize(Assembly[] assemblies)
        {
            if (DiBootstrap.Instance == null)
            {
                Debug.LogWarning("{EN} ContractsServer services were not created. DI is not ready.");
                return;
            }

            DiBootstrap.Instance.Resolve<INavigationProviderService>();
        }
    }
}
