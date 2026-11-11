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
        private float _bodyWidth;
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

        /// <summary>
        /// <para>{EN} Every target change goes through here: previous target, new target. Either may be null.</para>
        /// {RU} Через него проходит каждая смена цели: прежняя цель, новая цель. Любая может быть null.
        /// </summary>
        public event Action<UnitRuntime, UnitRuntime> TargetChanged;

        public UnitAggression(
            TargetSelector targetSelector,
            UnitAggressionData unitAggressionData)
        {
            _targetSelector = targetSelector;

            _id = unitAggressionData.Id;
            _radiusDetection = unitAggressionData.DetectionRadius;
            AttackRadius = unitAggressionData.AttackRadius;
            _bodyWidth = unitAggressionData.BodyWidth;
        }

        /// <summary>
        /// <para>{EN} Attack reach to this target between centers: the attack radius plus the mean body width of the unit and the target,
        /// so the radius counts from the body edges. Every attack distance check and every attack place uses it.</para>
        /// {RU} Дальность атаки до этой цели между центрами: радиус атаки плюс средняя ширина тел юнита и цели,
        /// то есть радиус считается от краёв тел. Через неё идут все проверки дистанции атаки и выбор места для атаки.
        /// </summary>
        public float GetAttackReach(UnitRuntime target)
        {
            return AttackRadius + (_bodyWidth + target.BodyWidth) * 0.5f;
        }

        /// <summary>
        /// <para>{EN} True if from this point the unit reaches the target by GetAttackReach.</para>
        /// {RU} true, если из этой точки юнит достаёт цель по GetAttackReach.
        /// </summary>
        public bool IsInAttackReach(Vector2 positionUnit, UnitRuntime target)
        {
            float reach = GetAttackReach(target);
            return reach * reach > Vector2.DistanceSqr(positionUnit, target.Position);
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
                SetTarget(target);
                float reach = GetAttackReach(target);
                SetStatus(reach * reach > distanceSqr ? StatusUnit.Attack : StatusUnit.MoveTarget);
                return true;
            }

            // Неудачный поиск может вернуть юнита вне радиуса — это не цель.
            SetTarget(null);
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
            SetTarget(null);
            SetStatus(StatusUnit.Idle);
        }

        private void SetTarget(UnitRuntime target)
        {
            if (ReferenceEquals(_currentTarget, target))
                return;

            UnitRuntime previous = _currentTarget;
            _currentTarget = target;
            TargetChanged?.Invoke(previous, target);
        }

        private bool IsInAttackRadius(Vector2 positionUnit)
        {
            return IsInAttackReach(positionUnit, _currentTarget);
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
