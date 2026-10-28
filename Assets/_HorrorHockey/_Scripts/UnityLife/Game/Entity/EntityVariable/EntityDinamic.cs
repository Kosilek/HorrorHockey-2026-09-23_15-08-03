using Shared.Core.Contracts;
using UnityEngine;
using UnityLife.Game.Entity.Navigations;
using FMVector3 = FM.Mathematics.DataTypes.Vector3;

namespace UnityLife.Game.Entity
{
    [RequireComponent(typeof(AgentAI))]
    public class EntityDinamic : Entity, IEntityMuvemet
    {
        #region Component
        protected AgentAI _agentAI;
        #endregion

        #region Public API
        public override void Initialize()
        {
            base.Initialize();

            _agentAI = GetComponent<AgentAI>();
            _agentAI.Initialize(_animator);
        }

        public void StartMovement(FMVector3[] vector3s)
        {
            _agentAI.StartMovement(vector3s);
        }
        #endregion
    }
}