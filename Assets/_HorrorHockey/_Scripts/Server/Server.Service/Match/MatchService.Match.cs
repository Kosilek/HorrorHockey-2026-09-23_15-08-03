using ContractsServer.Core.Enum;
using Server.Shared.Core.Contracts;

namespace Server.Service.Match
{
    public partial class MatchService : IMatchService
    {
        public void SpawnUnit(Team team, PlayerIndex playerIndex)
        {
            if (guidTest == null || !_matchs.TryGetValue(guidTest, out var session))
            {
                UnityEngine.Debug.LogWarning("MatchService: SpawnUnit - no active match. Start the match first.");
                return;
            }

            session.Controller.SpawnUnit(team, playerIndex);

        }
    }
}
