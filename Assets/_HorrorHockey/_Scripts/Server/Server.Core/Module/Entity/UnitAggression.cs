using FM.Mathematics.DataTypes;
using Server.Core.Data;
using Server.Core.Entity;
using Server.Core.Enums;
using Server.Core.Targets;
using System;
using static UnityEngine.GraphicsBuffer;

namespace Server.Core.Module.Entity
{
    public class UnitAggression
    {
        #region Component
        private TargetSelector _targetSelector;
        #endregion

        #region Fields Unit 
        private int _id;
        private float _radiusDetection;
        private float _radiusAttackSqr;
        public float AttackRadius { get; private set; }
        #endregion

        private UnitRuntime _currentTarget;
        public UnitRuntime CurrentTarget => _currentTarget;

        /// <summary>
        /// <para>{EN} True while the target is set and alive.</para>
        /// {RU} true, пока цель задана и жива.
        /// </summary>
        public bool HasTarget => _currentTarget != null && !_currentTarget.IsDead;

        public StatusUnit StatusUnit { get; private set; }
        public StateTarget StateTarget { get; private set; }

        /// <summary>
        /// <para>{EN} Every status change goes through here: previous status, new status.</para>
        /// {RU} Через него проходит каждая смена статуса: прежний статус, новый статус.
        /// </summary>
        public event Action<StatusUnit, StatusUnit> StatusChanged;

        public UnitAggression(
            TargetSelector targetSelector,
            UnitAggressionData unitAggressionData)
        {
            _targetSelector = targetSelector;

            _id = unitAggressionData.Id;
            _radiusDetection = unitAggressionData.DetectionRadius;
            AttackRadius = unitAggressionData.AttackRadius;
            _radiusAttackSqr = AttackRadius * AttackRadius;
        }

        public void SetStatusUnit(StatusUnit statusUnit)
        {
            SetStatus(statusUnit);
        }

        public bool CheckTarget(Int2 currentCell)
        {
            if (_targetSelector.TrySelectorTargetUnit(
                currentCell,
                _id,
                _radiusDetection,
                out UnitRuntime target,
                out float distanceSqr))
            {
                _currentTarget = target;
                SetStatus(_radiusAttackSqr > distanceSqr ? StatusUnit.Attack : StatusUnit.MoveTarget);
                return true;
            }

            // Неудачный поиск может вернуть юнита вне радиуса — это не цель.
            _currentTarget = null;
            return false;
        }

        public void CheckDistanceToTarget(Vector2 positionUnit)
        {
            if (CurrentTarget == null)
                return;

            if (IsInAttackRadius(positionUnit))
                SetStatus(StatusUnit.Attack);
        }

        /// <summary>
        /// <para>{EN} Attack inside the attack radius, MoveTarget outside. False without a live target, the status is not touched then.</para>
        /// {RU} Attack внутри радиуса атаки, MoveTarget снаружи. false без живой цели, статус тогда не меняется.
        /// </summary>
        public bool UpdateStatusByDistance(Vector2 positionUnit)
        {
            if (!HasTarget)
                return false;

            SetStatus(IsInAttackRadius(positionUnit) ? StatusUnit.Attack : StatusUnit.MoveTarget);
            return true;
        }

        /// <summary>
        /// <para>{EN} Forgets the target and goes Idle. A dead unit stays dead.</para>
        /// {RU} Забывает цель и переходит в Idle. Мёртвый юнит остаётся мёртвым.
        /// </summary>
        public void ClearTarget()
        {
            _currentTarget = null;
            SetStatus(StatusUnit.Idle);
        }

        private bool IsInAttackRadius(Vector2 positionUnit)
        {
            return _radiusAttackSqr > Vector2.DistanceSqr(positionUnit, _currentTarget.Position);
        }

        private void SetStatus(StatusUnit status)
        {
            if (StatusUnit == status || StatusUnit == StatusUnit.Die)
                return;

            StatusUnit previous = StatusUnit;
            StatusUnit = status;
            StatusChanged?.Invoke(previous, status);
        }
    }
}
