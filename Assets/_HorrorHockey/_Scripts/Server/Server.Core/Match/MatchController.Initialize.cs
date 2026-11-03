namespace Server.Core.Match
{
    public partial class MatchController
    {
        public void StartMatch()
        {
            _matchTimer.Restart();
            _matchTimer.Start();
        }

        public void EndMatch()
        {
            _matchTimer.Stop();
        }
    }
}