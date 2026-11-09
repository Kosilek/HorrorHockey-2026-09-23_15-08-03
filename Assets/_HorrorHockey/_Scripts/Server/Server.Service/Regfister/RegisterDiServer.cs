using _FM.DI;
using ContractsServer.Transport;
using FM.General.Core.Interfaces.Contracts;
using Server.Core.Entity;
using Server.Core.Interfaces;
using Server.ObjectPooling.Service;
using Server.Service.Match;
using Server.Service.Navigation;
using Server.Shared.Configurations;
using Server.Shared.Core.Contracts;
using Server.Shared.Core.Contracts.WebSocket;
using Server.WebSocket.Service;
using Server.WebSocket.Transport;
using System;
using UnityEngine;

namespace Server.Service.Register
{
    public class RegisterDiServer : IRegisterInDI
    {
        public void AddRegistrations(IRegistrationContext context)
        {
            ObjectPoolingConfiguration _objectPoolingService = new ObjectPoolingConfiguration
            {
                StartPoolSize = 12,
                MaxPoolSize = 16,
                PercentageAdd = 0.5f,
                PercentageToReduce = 0.7f,
                PercentageReduction = 0.25f,
                DurationDelete = 30
            };

            context.RegisterSingleton<ObjectPoolingConfiguration>(_objectPoolingService);
            context.RegisterSingleton<INavigationService, NavigationService>();
            context.RegisterSingleton<IMatchService, MatchService>();
            context.RegisterSingleton<IServerToClientTransport, InProcessServerToClient>();
            context.RegisterSingleton<IWebSocketService, WebSocketService>();
            context.RegisterSingleton<Func<IWebSocketService>>(() => DiBootstrap.Instance.Resolve<IWebSocketService>());

            // Registrations are deferred: DiBootstrap.Instance is null here, so resolve lazily.
            context.RegisterTransient<IWebSocketServiceUnit>(() => DiBootstrap.Instance.Resolve<IWebSocketService>());
            context.RegisterTransient<IWebSocketServiceMatch>(() => DiBootstrap.Instance.Resolve<IWebSocketService>());
        }
    }
}