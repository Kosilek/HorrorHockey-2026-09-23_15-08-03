using Shared.Core.Contracts.Domain;
using Shared.Core.Interfaces;
using UnityEngine;

namespace Domain.Game.Services
{
    public class ObjectLifeCicleService : IObjectLifeCicleService
    {
        public MonoBehaviour IntantiateObject(IInstObj instObj)
        {
            var obj = Object.Instantiate(instObj.Mono) as IInstObj;
            obj.Initialize();
            return obj.Mono;
        }
    }
}
