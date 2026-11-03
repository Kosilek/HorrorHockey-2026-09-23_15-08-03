using Server.Core.Interfaces;
using Server.Core.Match;
using Server.Shared.Configurations;
using Server.Shared.Core.Contracts;
using Server.Shared.Core.Contracts.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using static Codice.Client.BaseCommands.BranchExplorer.ExplorerData.BrExTreeBuilder.BrExFilter;

namespace Server.Service.Match
{
    public partial class MatchService : IMatchService
    {
        #region Components
        private readonly INavigationService _navigationService;
        private readonly ObjectPoolingConfiguration _configPool;
        // Resolved lazily: WebSocketService depends on IMatchService, so a direct constructor dependency is circular.
        private readonly Func<IWebSocketService> _webSocketService;
        #endregion

        private string guidTest;

        private readonly ConcurrentDictionary<string, MatchSession> _matchs;

        public MatchService(
            INavigationService navigationService,
            ObjectPoolingConfiguration configPool,
            Func<IWebSocketService> webSocketService)
        {
            _navigationService = navigationService;
            _configPool = configPool;
            _webSocketService = webSocketService;

            _matchs = new();


#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayMode;
            Application.quitting += StopAll;
#endif
        }

#if UNITY_EDITOR
        private void OnPlayMode(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                StopAll();
        }
#endif

        private void StopAll()
        {
            foreach (MatchSession session in _matchs.Values)
                session.Stop();
            foreach (MatchSession session in _matchs.Values)
                session.Join();
            _matchs.Clear();
        }

        #region Public API
        public void AddMatch()
        {
            string guid = Guid.NewGuid().ToString("N");
            guidTest = guid;
            MatchController match = new(
                _navigationService,
                _configPool,
                _webSocketService());
            MatchSession matchSession = new(match);

            if (!_matchs.TryAdd(guid, matchSession))
            {
                // Log Error
                return;
            }

            StartMatch(matchSession);
            Debug.Log($"MatchService: AddMatch - Match with GUID {guid} added and started.");
        }

        public void RemoveMatch(string guid)
        {
            if (!_matchs.TryRemove(guid, out var matchSession))
            {
                // Log warrning. Missing mnatch
                return;
            }

            EndMatch(matchSession);
            matchSession = null;
        }
        #endregion

        #region Private Helpers
        private void StartMatch(MatchSession matchSession)
        {
            ///

            matchSession.Start();
        }

        private void EndMatch(MatchSession matchSession)
        {
            ///

            matchSession.Stop();
            matchSession.Join();
        }
        #endregion
    }
}