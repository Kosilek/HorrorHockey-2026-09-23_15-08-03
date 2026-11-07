using ContractsServer.Core.Enum;
using ContractsServer.Service.Navigation;
using FM.Mathematics.DataTypes;
using Server.Core.Module.Entity;
using Server.Shared.Configurations;
using System;
using FMVector2 = FM.Mathematics.DataTypes.Vector2;

namespace Server.Core.Entity
{
    public partial class UnitRuntime : IPoolObj
    {
        #region Component
        private UnitAggression _unitAggression;
        #endregion 

        #region Fields Player
        public ushort Id { get; private set; }
        public Team Team { get; private set; }
        public PlayerIndex PlayerIndex { get; private set; }
        #endregion

        #region Fields Unit
        public Byte2 IndexUnit { get; private set; }
        public FMVector2 Position => _routeMotion.Position;
        public bool IsMoving => _routeMotion.IsMoving;
        public bool IsDynamic { get; private set; }

        public Func<FMVector2, int, Int2, Int2> _funcUpdateCell;
        public Int2 CurrentCell { get; set; }

        private int _currentTick = 0;
        private const int _maxTick = 4;
        #endregion

        #region Fields Route
        private FMVector2[] _baseRoute;
        private bool _followBaseRoute;

        /// <summary>
        /// <para>{EN} Start of the current route. The client starts its curve from here.</para>
        /// {RU} Старт текущего маршрута. Отсюда клиент начинает свою кривую.
        /// </summary>
        public FMVector2 RouteStart { get; private set; }

        /// <summary>
        /// <para>{EN} Controls of the current route. Together with RouteStart this is all the client needs.</para>
        /// {RU} Контролы текущего маршрута. Вместе с RouteStart это всё, что нужно клиенту.
        /// </summary>
        public FMVector2[] RouteControls { get; private set; }
        #endregion

        #region Fields special
        private Action<string, object> _sendCommand;
        #endregion

        #region Component
        private readonly UnitRouteMotion _routeMotion;
        #endregion
    }
}
