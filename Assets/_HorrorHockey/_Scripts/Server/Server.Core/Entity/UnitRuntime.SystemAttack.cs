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
            UnityEngine.Debug.Log("StartAttackLoop");
            _isStartAttackLoop = true;
            _entityDataAttackClip = GetCurrentClip();
            _currentTimeClip = 0;
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
            UnityEngine.Debug.Log($"ClipLoopAttack. Id {Id}. _isStartAttackLoop {_isStartAttackLoop}. _currentTimeClip {_currentTimeClip} _entityDataAttackClip?.Duration {_entityDataAttackClip?.Duration}. _entityDataAttackClip?.TickDamage {_entityDataAttackClip?.TickDamage}");
            if (!_isStartAttackLoop)
                return;

            if (_currentTimeClip >= _entityDataAttackClip?.Duration)
            {
                StopAttackClip();
            }

            if (_currentTimeClip < _entityDataAttackClip?.TickDamage + 0.1 && _currentTimeClip > _entityDataAttackClip?.TickDamage - 0.1f)
            {
                _unitAggression.CurrentTarget?.TakeDamage(Damage);
            }

            _currentTimeClip += _defaultTick;
        }

        public bool TakeDamage(float damage)
        {
            Health -= damage;

            UnityEngine.Debug.Log($"TakeDamage. Id: {Id}. Health {Health}");

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
