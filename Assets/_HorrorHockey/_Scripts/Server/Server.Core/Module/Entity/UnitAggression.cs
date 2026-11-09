using FM.Mathematics.DataTypes;
using Server.Core.Data;
using Server.Core.Entity;
using Server.Core.Enums;
using Server.Core.Targets;
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
        #endregion

        private UnitRuntime _currentTarget;
        public UnitRuntime CurrentTarget => _currentTarget;

        public StatusUnit StatusUnit { get; private set; }
        public StateTarget StateTarget { get; private set; }

        public UnitAggression(
            TargetSelector targetSelector,
            UnitAggressionData unitAggressionData)
        {
            _targetSelector = targetSelector;

            _id = unitAggressionData.Id;
            _radiusDetection = unitAggressionData.DetectionRadius;
            _radiusAttackSqr = unitAggressionData.AttackRadius * unitAggressionData.AttackRadius;
        }

        public bool CheckTarget(Int2 currentCell)
        {
            if (_targetSelector.TrySelectorTargetUnit(
                currentCell,
                _id,
                _radiusDetection,
                out _currentTarget,
                out float distanceSqr))
            {
                if (_radiusAttackSqr > distanceSqr)
                {
                   // StatusUnit = StatusUnit.Attack;
                }
                else
                {
                    StatusUnit = StatusUnit.MoveTarget;
                }
                return true;
            }

            return false;
        }

        public void CheckDistanceToTarget(Vector2 positionUnit)
        {
            if (CurrentTarget == null)
                return;

            float distanceSqr = Vector2.DistanceSqr(
                        positionUnit,
                        CurrentTarget.Position);

            if (_radiusAttackSqr > distanceSqr)
            {
            //    StatusUnit = StatusUnit.Attack;
            }
        }

    }
}
