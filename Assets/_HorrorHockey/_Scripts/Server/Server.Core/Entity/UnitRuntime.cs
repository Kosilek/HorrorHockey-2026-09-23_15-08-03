using ContractsServer.Core.Enum;
using ContractsServer.Service.Navigation;
using FM.Mathematics.DataTypes;
using Server.Shared.Configurations;
using System;

namespace Server.Core.Entity
{
    public partial class UnitRuntime : IPoolObj
    {
        #region Fields Player
        public ushort Id { get; private set; }
        public Team Team { get; private set; }
        public PlayerIndex PlayerIndex { get; private set; }
        #endregion

        #region Fields Unit
        public Byte2 indexUnit { get; private set; }
        #endregion

        #region Fields Route
        private Vector2[] _baseRoute;
        private bool _followBaseRoute;

        /// <summary>
        /// <para>{EN} Start of the current route. The client starts its curve from here.</para>
        /// {RU} Старт текущего маршрута. Отсюда клиент начинает свою кривую.
        /// </summary>
        public Vector2 RouteStart { get; private set; }

        /// <summary>
        /// <para>{EN} Controls of the current route. Together with RouteStart this is all the client needs.</para>
        /// {RU} Контролы текущего маршрута. Вместе с RouteStart это всё, что нужно клиенту.
        /// </summary>
        public Vector2[] RouteControls { get; private set; }
        #endregion

        #region Fields special
        private Action<string, object> _sendCommand;
        #endregion

        #region Component
        private readonly UnitRouteMotion _routeMotion;
        #endregion
    }
}
