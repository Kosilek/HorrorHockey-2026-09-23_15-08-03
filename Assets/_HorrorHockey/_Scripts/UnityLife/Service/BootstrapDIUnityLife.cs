using _FM.DI;
using FM.General.Core.Interfaces.Contracts;
using Shared.Core.Contracts.Domain;
using Shared.Data.Provider;
using System.Reflection;
using UnityEngine;

namespace UnityLife.Service
{
    public class BootstrapDIUnityLife : IBootstrap
    {
        public void Initialize(Assembly[] assemblies)
        {
            if (DiBootstrap.Instance == null)
            {
                Debug.LogWarning("{EN} Domain services were not created. DI is not ready.");
                return;
            }

            DiBootstrap.Instance.Resolve<ProviderService>();
        }
    }
}
