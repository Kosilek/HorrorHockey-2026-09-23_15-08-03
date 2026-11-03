using UnityEngine;
using UnityLife.Game.Entity.Navigations;

namespace UnityLife.Game.Entity.Animations
{
    /// <summary>
    /// <para>{EN} Rolls the body into the turn from AgentAI.CurrentTilt. Feet stick to the ground layer. Put this on the same object as the Animator. The layer needs IK Pass.</para>
    /// {RU} Наклоняет тело в поворот по AgentAI.CurrentTilt. Стопы сажает на слой земли. Вешается на объект с Animator. У слоя нужен IK Pass.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class EntityBodyTilt : MonoBehaviour
    {
        [SerializeField] private LayerMask _groundMask;
        [SerializeField, Min(0.05f)] private float _rayHeight = 0.5f;
        [SerializeField, Min(0.05f)] private float _rayDistance = 1.5f;
        [SerializeField] private float _footOffset = 0.25f;

        private Animator _animator;
        private AgentAI _agentAI;

        private void Reset()
        {
            _groundMask = LayerMask.GetMask("Ground", "GroundConnect");
        }

        #region Inititalize
        public void Inititalize(Animator animator, AgentAI agentAI)
        {
            _animator = animator;
            _agentAI = agentAI;
        }

        #endregion end Inititalize

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || !_animator.isHuman)
                return;

            float currentTilt = _agentAI.CurrentTilt;
            if (Mathf.Abs(currentTilt) >= 0.01f)
            {
                Transform root = _agentAI.transform;
                Quaternion tilt = Quaternion.AngleAxis(-currentTilt, root.forward);
                Vector3 pivot = root.position;
                _animator.bodyPosition = pivot + tilt * (_animator.bodyPosition - pivot);
                _animator.bodyRotation = tilt * _animator.bodyRotation;
            }

            PlantFoot(AvatarIKGoal.LeftFoot);
            PlantFoot(AvatarIKGoal.RightFoot);
        }

        private void PlantFoot(AvatarIKGoal goal)
        {
            Vector3 foot = _animator.GetIKPosition(goal);
            Quaternion rotation = _animator.GetIKRotation(goal);
            if (TryGround(foot, out RaycastHit hit))
            {
                foot.y = hit.point.y + _footOffset;
                Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, hit.normal);
                if (forward.sqrMagnitude > 1e-6f)
                    rotation = Quaternion.LookRotation(forward.normalized, hit.normal);
            }

            _animator.SetIKPositionWeight(goal, 1f);
            _animator.SetIKRotationWeight(goal, 1f);
            _animator.SetIKPosition(goal, foot);
            _animator.SetIKRotation(goal, rotation);
        }

        private bool TryGround(Vector3 foot, out RaycastHit hit)
        {
            hit = default;

            Vector3 origin = foot + Vector3.up * _rayHeight;
            float distance = _rayHeight + _rayDistance;
            return Physics.Raycast(origin, Vector3.down, out hit, distance, _groundMask, QueryTriggerInteraction.Ignore);
        }
    }
}