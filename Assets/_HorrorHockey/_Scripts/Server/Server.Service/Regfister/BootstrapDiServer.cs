using _FM.DI;
using FM.General.Core.Interfaces.Contracts;
using Server.Core.Interfaces;
using Server.Shared.Core.Contracts;
using System.Reflection;
using UnityEngine;

namespace Server.Service.Register
{
    public class BootstrapDiServer : IBootstrap
    {
        public void Initialize(Assembly[] assemblies)
        {
            if (DiBootstrap.Instance == null)
            {
                Debug.LogWarning("{EN} Domain services were not created. DI is not ready.");
                return;
            }

            DiBootstrap.Instance.Resolve<INavigationService>();
            DiBootstrap.Instance.Resolve<IMatchService>();
        }
    }
}