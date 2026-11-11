using ContractsServer.Data.Entity;
using FM.Mathematics.DataTypes;
using Server.Core.Enums;
using System;

namespace Server.Core.Entity
{
    public partial class UnitRuntime
    {
        private bool _isStartAttackLoop = false;
        private EntityDataAttackClip _entityDataAttackClip;
        private float _currentTimeClip;

        private void StartAttackLoop()
        {
            if (_isStartAttackLoop)
                return;
            _isStartAttackLoop = true;
            _entityDataAttackClip = GetCurrentClip();
            _currentTimeClip = 0;
            StartAnimationClip((byte)StateAnimation.Attack);
            ClipLoopAttack();
        }

        private EntityDataAttackClip GetCurrentClip()
        {
            int totalWeight = 0;
            foreach (var item in EntityDataAttackClips)
            {
                totalWeight += item.Weight;
            }

            if (totalWeight <= 0)
                return EntityDataAttackClips[0];

            int rand = _random.Next(0, totalWeight);

            foreach (var item in EntityDataAttackClips)
            {
                rand -= item.Weight;

                if (rand < 0)
                {
                    return item;
                }
            }

            return EntityDataAttackClips[0];
        }

        private void StopAttackClip()
        {
            _isStartAttackLoop = false;
            _entityDataAttackClip = null;
            _currentTimeClip = 0f;
        }

        private void ClipLoopAttack()
        {
            if (!_isStartAttackLoop || _entityDataAttackClip == null)
                return;

            float duration = _entityDataAttackClip.Duration;
            if (duration <= 0f)
                return;

            float previous = _currentTimeClip;
            float next = previous + _defaultTick;
            if (CrossedDamageTick(previous, next, _entityDataAttackClip.TickDamage, duration))
                _unitAggression.CurrentTarget?.TakeDamage(Damage);

            if (next < duration)
            {
                _currentTimeClip = next;
                return;
            }

            _currentTimeClip = next - duration;
            StartAnimationClip((byte)StateAnimation.Attack);
        }

        // Один удар за цикл. Попадание считается и на шаге, который перешагивает конец клипа.
        private static bool CrossedDamageTick(float previous, float next, float hit, float duration)
        {
            if (hit < 0f || hit > duration)
                return false;

            if (next < duration)
                return previous < hit && next >= hit || hit == 0f && previous == 0f;

            return previous < hit || next - duration >= hit;
        }

        public bool TakeDamage(float damage)
        {
            Health -= damage;

            UnityEngine.Debug.Log($"TakeDamage. Id: {Id}. Health: {Health}");
            if (Health <= 0)
            {
                _unitAggression.SetStatusUnit(StatusUnit.Die);
                return false;
            }

            return true;

        }

        public void TakeHealing(float healing)
        {
            Health = Math.Min(Health + healing, _maxHealth);
        }
    }
}
