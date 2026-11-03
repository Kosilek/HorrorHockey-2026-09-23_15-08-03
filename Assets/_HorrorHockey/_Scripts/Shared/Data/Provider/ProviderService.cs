using UnityEngine;

namespace Shared.Data.Provider
{

    [CreateAssetMenu(fileName = "ProviderService", menuName = "Provider/ProviderService")]
    public class ProviderService : ScriptableObject
    {
        public void Initialize()
        {
            if (_providerUnitService == null)
            {
                Debug.LogError($"{nameof(ProviderService)}: {nameof(_providerUnitService)} is not assigned.", this);
                return;
            }

            _providerUnitService.Initialize();
        }

        [SerializeField] private ProviderUnitService _providerUnitService;
        public ProviderUnitService ProviderUnitService => _providerUnitService;
    }
}