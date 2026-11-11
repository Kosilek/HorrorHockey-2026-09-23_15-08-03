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
            _agentAI.StartMovement(points);
        }

        public void StartMovement(FMVector2 start, FMVector2[] controls)
        {
            if (_agentAI == null)
                Initialize();
            _agentAI.StartMovement(start, controls);
        }

        public void StartMovement(FMVector2 start, FMVector2[] controls, float startSpeed)
        {
            if (_agentAI == null)
                Initialize();
            _agentAI.StartMovement(start, controls, startSpeed);
        }

        public void StartMovingRoute(FMVector2 start, FMVector2[] controls, float startSpeed)
        {
            if (_agentAI == null)
                Initialize();
            _agentAI.StartMovingRoute(start, controls, startSpeed);
        }

        public void SlowTo(float speed)
        {
            if (_agentAI == null)
                Initialize();
            _agentAI.SlowTo(speed);
        }

        public void PauseMovement()
        {
            if (_agentAI == null)
                Initialize();
            _agentAI.PauseMovement();
        }

        public void ResumeMovement()
        {
            if (_agentAI == null)
                Initialize();
            _agentAI.ResumeMovement();
        }

        public void StopMovement()
        {
            if (_agentAI == null)
                Initialize();
            _agentAI.StopMovement();
        }

        public void StartAnimationClip(byte animationClip)
        {
            if (_agentAI == null)
                Initialize();
            _agentAI.StartAnimationClip(animationClip);
        }

        public void SetTarget(IEntityMovemet target)
        {
            if (_agentAI == null)
                Initialize();
            _agentAI.SetTarget(target);
        }

        public FMVector2 BodyPosition => new FMVector2(transform.position.x, transform.position.z);
        #endregion
    }
}