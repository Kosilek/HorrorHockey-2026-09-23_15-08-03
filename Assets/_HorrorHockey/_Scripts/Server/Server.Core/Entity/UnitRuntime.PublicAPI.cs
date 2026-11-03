using ContractsServer.Command;
using UnityEngine;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace Server.Core.Entity
{
    public partial class UnitRuntime
    {
        #region Public API
        public FMVector2 Position => _routeMotion.Position;
        public bool IsMoving => _routeMotion.IsMoving;

        public void Tick(float deltaTime)
        {
            if (_routeMotion.HasRoute)
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

        /// <summary>
        /// <para>{EN} Leaves the lane and follows these controls from the current position. Stops at the end.</para>
        /// {RU} Сходит с полосы и идёт по этим контролам от текущей позиции. В конце останавливается.
        /// </summary>
        public bool TryStartRoute(FMVector2[] controls)
        {
            if (!TryBeginRoute(controls))
                return false;

            _followBaseRoute = false;
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

        private bool TryBeginRoute(FMVector2[] controls)
        {
            FMVector2 start = _routeMotion.Position;
            if (!_routeMotion.TryStart(start, controls))
                return false;

            RouteStart = start;
            RouteControls = controls;
            return true;
        }
    }
}
