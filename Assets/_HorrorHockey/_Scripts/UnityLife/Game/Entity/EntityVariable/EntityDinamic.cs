using Shared.Core.Contracts;
using Shared.Core.Interfaces;
using UnityEngine;
using UnityLife.Game.Entity.Animations;
using UnityLife.Game.Entity.Navigations;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace UnityLife.Game.Entity
{
    [RequireComponent(typeof(AgentAI))]
    [RequireComponent(typeof(EntityBodyTilt))]
    public class EntityDinamic : Entity, IEntityMovemet, IInstObj
    {
        #region Fields public
        public MonoBehaviour Mono => this;
        #endregion

        #region Component
        protected AgentAI _agentAI;
        protected EntityBodyTilt _entityBodyTilt;
        #endregion

        #region Public API
        public override void Initialize()
        {
            base.Initialize();

            _agentAI = GetComponent<AgentAI>();
            _agentAI.Initialize(_animator);
            _entityBodyTilt = GetComponent<EntityBodyTilt>();
            _entityBodyTilt.Inititalize(_animator, _agentAI);
        }

        public void StartMovement(FMVector2[] points)
        {
            if (_agentAI == null)
                Initialize();
            Debug.Log($"EntityDinamic: StartMovement with {points.Length} points.");
            _agentAI.StartMovement(points);
        }

        public void StartMovement(FMVector2 start, FMVector2[] controls)
        {
            if (_agentAI == null)
                Initialize();
            Debug.Log($"EntityDinamic: StartMovement from {start} with {controls.Length} controls.");
            _agentAI.StartMovement(start, controls);
        }
        #endregion
    }
}