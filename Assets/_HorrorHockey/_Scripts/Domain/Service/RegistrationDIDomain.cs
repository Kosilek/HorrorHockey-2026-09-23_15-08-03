using _FM.DI;
using ContractsServer.Transport;
using Domain.Data;
using Domain.Game.Services;
using Domain.Server.Service;
using Domain.Server.Transport;
using FM.General.Core.Interfaces.Contracts;
using Shared.Core.Contracts;
using Shared.Core.Contracts.Domain;
using Shared.Core.Contracts.Server;
using System;

namespace Domain.Service
{
    public class RegistrationDIDomain : IRegisterInDI
    {
        
        public void AddRegistrations(IRegistrationContext context)
        {
            context.RegisterSingleton<INavigationMapData, NavigationMapData>();
            context.RegisterSingleton<INavigationService, NavigationService>();
            context.RegisterSingleton<IObjectLifeCicleService, ObjectLifeCicleService>();
            context.RegisterSingleton<IRuntimeService, RuntimeService>();
            context.RegisterSingleton<IClientToServerTransport, InProcessClientToServer>();
            context.RegisterSingleton<INetworkService, NetworkService>();
            context.RegisterSingleton<Func<INetworkService>>(() => DiBootstrap.Instance.Resolve<INetworkService>());
        }
    }
}
