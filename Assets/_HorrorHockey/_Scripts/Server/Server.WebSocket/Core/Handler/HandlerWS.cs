using Server.Shared.Core.Contracts;
using System;

namespace Server.WebSocket.Core.Handler
{
    internal partial class HandlerWS
    {
        private readonly IMatchService _matchService;
        private readonly Action<string, object> _send;

        public HandlerWS(
            IMatchService matchService,
            Action<string, object> send)
        {
            _matchService = matchService;
            _send = send;
        }
    }
}
