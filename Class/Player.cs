namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    public class Player
    {
        public string? PlayerName { get; set; }
        public ulong PlayerSteamID { get; set; }
        public int Team { get; set; }
        public int Score { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Damage { get; set; }

        // KDA Ratio: (Kills + Assists) / Deaths
        public float KDA => Deaths == 0 ? Kills : (float)Kills / Deaths;

        // Performance score based on KDA, Damage, and Score
        public float PerformanceScore => KDA * 0.5f + Damage * 0.3f + Score * 0.2f;

        // Track the round number when the player was last moved
        public int LastMovedRound { get; set; } = 0;
    }
}

