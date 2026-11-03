using UnityEngine;

namespace Shared.Core.Interfaces
{
    public interface IInstObj
    {
        public MonoBehaviour Mono { get; }
        void Initialize();
    }
}
