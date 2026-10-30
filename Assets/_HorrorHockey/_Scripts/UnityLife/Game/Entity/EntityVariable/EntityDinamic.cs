using Shared.Core.Contracts;
using UnityEngine;
using UnityLife.Game.Entity.Navigations;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace UnityLife.Game.Entity
{
    [RequireComponent(typeof(AgentAI))]
    public class EntityDinamic : Entity, IEntityMovemet
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

        public void StartMovement(FMVector2[] points)
        {
            if (_agentAI == null)
                Initialize();
            _agentAI.StartMovement(points);
        }
        #endregion
    }
}