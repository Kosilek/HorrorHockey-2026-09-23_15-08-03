using ContractsServer.Command;
using Server.Core.Enums;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace Server.Core.Entity
{
    public partial class UnitRuntime
    {
        private FMVector2 savePositionTarget;

        #region Public API

        public void Tick(float deltaTime)
        {
            if (_unitAggression.StatusUnit == StatusUnit.Die)
                return;

            if (_unitAggression.StatusUnit != StatusUnit.Attack)
            {
                if (_routeMotion.HasRoute) // Где - то в этом if\else нужно будет добавить логику флага
                {
                    _routeMotion.Tick(deltaTime);
                }
                else if (_followBaseRoute)
                {
                    if (TryBeginRoute(_baseRoute))
                    {
                        _sendCommand.Invoke(CommandWs.CreateRoute, (Id, RouteStart, RouteControls));
                    }
                }
            }

            
            switch (_currentTick)
            {
                case 0:
                    if (_unitAggression.StatusUnit == StatusUnit.MoveTarget)
                    {
                        if (savePositionTarget != _unitAggression.CurrentTarget.Position)
                        {
                            FMVector2[] controls = new FMVector2[1];
                            controls[0] = _unitAggression.CurrentTarget.Position;
                            savePositionTarget = controls[0];
                            TryStartMovingRoute(controls);
                        }
                    }
                    break;
                case 1:

                    break;
                case 2:
                    if (_unitAggression.StatusUnit is not (StatusUnit.Attack or StatusUnit.MoveTarget))
                    {
                        if (_unitAggression.CheckTarget(CurrentCell))
                        {
                            FMVector2[] controls = new FMVector2[1];
                            controls[0] = _unitAggression.CurrentTarget.Position;
                            savePositionTarget = controls[0];
                            if (_unitAggression.StatusUnit == StatusUnit.Idle)
                                TryStartRoute(controls);
                            else
                            {
                                TryStartMovingRoute(controls);
                            }
                        }
                    }
                        break;
                case 3:
                    if (_unitAggression.StatusUnit == StatusUnit.MoveTarget)
                    {
                        if (savePositionTarget != _unitAggression.CurrentTarget.Position)
                        {
                            FMVector2[] controls = new FMVector2[1];
                            controls[0] = _unitAggression.CurrentTarget.Position;
                            savePositionTarget = controls[0];
                            TryStartMovingRoute(controls);
                        }
                    }
                    break;
                case 4:
                    CurrentCell = _funcUpdateCell(Position, Id, CurrentCell);
                    break;
            }

            _currentTick++;
            if (_currentTick > _maxTick)
            {
                _currentTick = 0;
            }
        }

        /// <summary>
        /// <para>{EN} Leaves the lane and follows these controls from the current position. Stops at the end.</para>
        /// {RU} Сходит с полосы и идёт по этим контролам от текущей позиции. В конце останавливается.
        /// </summary>
        public bool TryStartRoute(FMVector2[] controls)
        {
            if (!TryBeginRoute(controls))
                return false;

            _followBaseRoute = false;
            _sendCommand.Invoke(CommandWs.CreateRoute, (Id, RouteStart, controls));
            return true;
        }

        /// <summary>
        /// <para>{EN} Leaves the lane and follows these controls from the current position. Keeps the current speed and does not brake at the end.</para>
        /// {RU} Сходит с полосы и идёт по этим контролам от текущей позиции. Сохраняет текущую скорость и в конце не тормозит.
        /// </summary>
        public bool TryStartMovingRoute(FMVector2[] controls)
        {
            float startSpeed = _routeMotion.CurrentSpeed;
            if (!TryBeginRoute(controls, stopAtEnd: false, startSpeed))
                return false;

            _followBaseRoute = false;
            _sendCommand.Invoke(CommandWs.CreateRoute, (Id, RouteStart, controls, false, _routeMotion.CurrentSpeed));
            return true;
        }

        /// <summary>
        /// <para>{EN} Back to the lane loop from the current position.</para>
        /// {RU} Возврат к циклу по полосе от текущей позиции.
        /// </summary>
        public void FollowBaseRoute()
        {
            _followBaseRoute = true;
            _routeMotion.Stop();
        }
        #endregion

        private bool TryBeginRoute(FMVector2[] controls, bool stopAtEnd = true, float startSpeed = 0f)
        {
            FMVector2 start = _routeMotion.Position;
            if (!_routeMotion.TryStart(start, controls, stopAtEnd, startSpeed))
                return false;

            RouteStart = start;
            RouteControls = controls;
            return true;
        }
    }
}
