using Shared.Core.Interfaces;
using UnityEngine;

namespace Shared.Core.Contracts.Domain
{
    public interface IObjectLifeCicleService
    {
        MonoBehaviour IntantiateObject(IInstObj instObj);
    }
}
