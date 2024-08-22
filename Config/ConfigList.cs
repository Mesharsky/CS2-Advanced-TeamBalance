namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    public class BalanceConfig
    {
        public required PluginSettingsConfig PluginSettings { get; set; }
    }

    public class PluginSettingsConfig
    {
        public int MinPlayers { get; set; } = 4;
        public float MaxScoreBalanceRatio { get; set; } = 1.6f;
        public bool UsePerformanceScore { get; set; } = true;
        public int MaxTeamSizeDifference { get; set; } = 1;
    }
}
