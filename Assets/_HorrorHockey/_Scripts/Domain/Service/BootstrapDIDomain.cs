using _FM.DI;
using FM.General.Core.Interfaces.Contracts;
using Shared.Core.Contracts;
using Shared.Core.Contracts.Domain;
using Shared.Core.Contracts.Server;
using System.Reflection;
using UnityEngine;

namespace Domain.Service
{
    public class BootstrapDIDomain : IBootstrap
    {
        public void Initialize(Assembly[] assemblies)
        {
            if (DiBootstrap.Instance == null)
            {
                Debug.LogWarning("{EN} Domain services were not created. DI is not ready.");
                return;
            }

            DiBootstrap.Instance.Resolve<INavigationMapData>();
            DiBootstrap.Instance.Resolve<INavigationService>();
            DiBootstrap.Instance.Resolve<IRuntimeService>();
            DiBootstrap.Instance.Resolve<INetworkService>();
        }
    }
}
