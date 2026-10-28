using UnityEngine;

namespace UnityLife.Game.Entity
{
    [RequireComponent(typeof(Animator))]
    public class Entity : MonoBehaviour
    {
        protected Animator _animator;

        #region Public API
        public virtual void Initialize()
        {
            _animator = GetComponent<Animator>();
        }
        #endregion
    }
}
