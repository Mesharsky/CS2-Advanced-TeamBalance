namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    public class BalanceConfig
    {
        public required PluginSettingsConfig PluginSettings { get; set; }
    }

    public class PluginSettingsConfig
    {
        public string PluginTag { get; set; } = "[red][TeamBalance][default]";
        public int MinPlayers { get; set; } = 4;
        public float MaxScoreBalanceRatio { get; set; } = 2.0f;
        public bool UsePerformanceScore { get; set; } = true;
        public int MaxTeamSizeDifference { get; set; } = 1;
        public bool EnableDebugMessages { get; set; } = true;
        public bool EnableChatMessages { get; set; } = true;
        public string ScrambleMode { get; set; } = "none";
        public int RoundScrambleInterval { get; set; } = 5;
        public int WinstreakScrambleThreshold { get; set; } = 3;
        public bool HalftimeScrambleEnabled { get; set; } = false;

        public void ValidateSettings()
        {
            if (ScrambleMode != "none")
            {
                UsePerformanceScore = false;
                Console.WriteLine("Performance Score disabled because scramble mode is enabled.");
            }
        }
    }
}
