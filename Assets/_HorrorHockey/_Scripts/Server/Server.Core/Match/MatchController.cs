using ContractsServer.Core.Interfaces;
using ContractsServer.Data;
using ContractsServer.Service.Entity;
using ContractsServer.Service.Navigation;
using Server.Core.Entity;
using Server.Core.Interfaces;
using Server.Core.Targets;
using Server.Core.WorldMap;
using Server.ObjectPooling.Service;
using Server.Shared.Configurations;
using Server.Shared.Core.Contracts.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace Server.Core.Match
{
    public partial class MatchController
    {
        #region Components
        private readonly INavigationService _navigationService;
        private readonly IWebSocketService _webSocket;
        private readonly IEntityProviderService _entityProviderService;


        private readonly IObjectPoolingService<UnitRuntime> _objectPoolingService;
        private readonly IWorldGrid _worldGrid;
        private TargetSelector _targetSelector;
        #endregion

        #region Fields
        private readonly float _deltaTime;
        #endregion

        #region Timer
        private readonly Stopwatch _matchTimer;
        private float _matchTime => (float)_matchTimer.Elapsed.TotalSeconds;
        #endregion

        #region Units
        private readonly Dictionary<int, UnitRuntime> _units;
        #endregion

        /// <summary>
        /// <para>{EN} Calls from other threads. Run at the start of Tick on the match thread.</para>
        /// {RU} Вызовы из других потоков. Выполняются в начале Tick в потоке матча.
        /// </summary>
        private readonly ConcurrentQueue<Action> _commands;

        public MatchController(
            INavigationService navigationService,
            ObjectPoolingConfiguration configPooling,
            IWebSocketService webSocketService,
            IWorldGrid worldGrid,
            IEntityProviderService entityProviderService)

        {
            _navigationService = navigationService;
            _objectPoolingService = new ObjectPoolingService<UnitRuntime>(configPooling);
            _deltaTime = GameConstants.DeltaTime;
            _webSocket = webSocketService;
            _worldGrid = worldGrid;
            _entityProviderService = entityProviderService;

            _targetSelector = new(_worldGrid, GetUnit);

            _units = new();
            _commands = new();
            _matchTimer = new();
        }
    }
}
