using ContractsServer.Core.Enum;
using FM.Mathematics.DataTypes;
using Server.Shared.Core.Contracts;

namespace Server.Service.Match
{
    public partial class MatchService : IMatchService
    {
        public void SpawnUnit(Byte2 typeUnit, Team team, PlayerIndex playerIndex)
        {
            if (guidTest == null || !_matchs.TryGetValue(guidTest, out var session))
            {
                UnityEngine.Debug.LogWarning("MatchService: SpawnUnit - no active match. Start the match first.");
                return;
            }

            session.Controller.SpawnUnit(typeUnit, team, playerIndex);

        }
    }
}
