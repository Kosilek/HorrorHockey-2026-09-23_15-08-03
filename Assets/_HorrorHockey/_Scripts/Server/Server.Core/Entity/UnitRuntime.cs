using ContractsServer.Core.Enum;
using ContractsServer.Data.Entity;
using ContractsServer.Service.Navigation;
using FM.Mathematics.DataTypes;
using Server.Core.Enums;
using Server.Core.Interfaces;
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
        private readonly UnitRouteMotion _routeMotion;
        private readonly Random _random;
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

        /// <summary>
        /// <para>{EN} Actual speed in meters per second, including accel and brake.</para>
        /// {RU} Фактическая скорость, м/с, с учётом разгона и торможения.
        /// </summary>
        public float CurrentSpeed => _routeMotion.CurrentSpeed;
        public bool IsDynamic { get; private set; }

        public Func<FMVector2, int, Int2, Int2> _funcUpdateCell;
        public Int2 CurrentCell { get; set; }
        public float BodyWidth { get; private set; }
        public float Mass { get; private set; }

        public float StunTime { get; private set; }

        private int _currentTick = 0;
        private const int _maxTick = 4;

        private float _maxHealth;
        public float Health { get; private set; }
        public float Defense { get; private set; }
        public float Damage { get; private set; }
        public EntityDataAttackClip[] EntityDataAttackClips { get; private set; }
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

        #region Fields Circle
        private const int CirclePointsPerLap = 16;

        private FMVector2[] _circleControls;
        private Action _circleComplete;

        /// <summary>
        /// <para>{EN} True while the circle route is the current one. Any other route replaces it.</para>
        /// {RU} true, пока текущий маршрут — круговой. Любой другой маршрут его заменяет.
        /// </summary>
        public bool IsCircling => _circleControls != null && ReferenceEquals(_circleControls, RouteControls);
        #endregion

        #region Fields Stun
        private int _stunCount;
        private float _stunTimer;

        /// <summary>
        /// <para>{EN} True while at least one Stun is not lifted.</para>
        /// {RU} true, пока хотя бы один Stun не снят.
        /// </summary>
        public bool IsStunned => _stunCount > 0;
        #endregion

        private bool CanRebuildRoute => !IsCircling && !IsStunned;

        // Сервер и клиент должны встать в одной точке, поэтому атака ждёт конца маршрута к радиусу атаки.
        private bool IsDrivingToAttackRange => _chaseToAttackRange && _routeMotion.HasRoute
            && _routeMotion.PathProgress < _routeMotion.PathLength - 1e-4f;

        /// <summary>
        /// <para>{EN} Same rule as Tick: in attack or dead the route is not advanced. An attack that starts as a charge keeps moving until the hit. A circle from that hit keeps moving until it ends.</para>
        /// {RU} То же правило, что в Tick: в атаке и после смерти маршрут не продвигается. Атака, начатая чарджем, едет до удара. Круг после этого удара едет, пока не закончится.
        /// </summary>
        private bool CanMoveByRoute => _unitAggression.StatusUnit switch
        {
            StatusUnit.Die => false,
            StatusUnit.Attack => _attackCharge || IsCircling || IsDrivingToAttackRange,
            _ => true
        };

        #region Fields special
        private Action<string, object> _sendCommand;
        private INavigationService _navigationService;
        #endregion
    }
}
