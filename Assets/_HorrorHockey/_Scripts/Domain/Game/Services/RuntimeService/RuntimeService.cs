using ContractsServer.Command;
using ContractsServer.Core.Interfaces;
using Shared.Data.Provider;
using Shared.Core.Contracts;
using Shared.Core.Contracts.Domain;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Domain.Game.Services
{
    public partial class RuntimeService : IRuntimeService
    {
        private readonly INavigationProviderService _navigationProviderService;
        private readonly IObjectLifeCicleService _objectLifeCicleService;
        private readonly ProviderService _providerService;

        private readonly Dictionary<ushort, IEntityMovemet> _entityMovements;

        public RuntimeService(
            INavigationProviderService navigationProviderService,
            IObjectLifeCicleService objectLifeCicleService,
            ProviderService providerService)
        {
            _navigationProviderService = navigationProviderService;
            _objectLifeCicleService = objectLifeCicleService;
            _providerService = providerService;
            _entityMovements = new();
        }
    }
}
