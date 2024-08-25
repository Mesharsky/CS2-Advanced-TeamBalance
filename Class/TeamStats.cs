namespace Mesharsky_TeamBalance;


public partial class Mesharsky_TeamBalance
{
    public class TeamStats
    {
        public List<PlayerStats> Stats { get; set; } = new List<PlayerStats>();
        public float TotalPerformanceScore { get; private set; }

        public void CalculatePerformanceScore()
        {
            TotalPerformanceScore = Stats.Sum(player => player.PerformanceScore);
        }

        public void Reset()
        {
            Stats.Clear();
            TotalPerformanceScore = 0;
        }

        public void AddPlayer(PlayerStats player)
        {
            Stats.Add(player);
            CalculatePerformanceScore();
        }

        public void RemovePlayer(PlayerStats player)
        {
            Stats.Remove(player);
            CalculatePerformanceScore();
        }
    }
}