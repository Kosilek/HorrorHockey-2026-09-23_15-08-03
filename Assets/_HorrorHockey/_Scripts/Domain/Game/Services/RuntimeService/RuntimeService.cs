using ContractsServer.Core.Interfaces;
using ContractsServer.Service.Navigation;
using Shared.Core.Contracts;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Domain.Game.Services
{
    public partial class RuntimeService : IRuntimeService
    {
        private readonly IEntityMovemet entityMovemet;
        private readonly INavigationProviderService _navigationProviderService;

        public RuntimeService(INavigationProviderService navigationProviderService)
        {
            var objects = Object.FindObjectsByType<MonoBehaviour>()
                .OfType<IEntityMovemet>()
                .ToArray();
            Debug.Log("qqq");
            _navigationProviderService = navigationProviderService;
            Debug.Log("qqq 2");
            entityMovemet = objects[0];
            Debug.Log("qqq 3");
            var path = _navigationProviderService.BasicRouteData.LeftRoute;
            Debug.Log("qqq 4");
            var points = path.Select(x => x.Position).ToArray();
            Debug.Log($"qqq 5 {entityMovemet}");

            entityMovemet.StartMovement(points);
            Debug.Log("qqq 6");
        }
    }
}
