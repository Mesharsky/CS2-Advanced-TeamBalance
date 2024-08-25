namespace Mesharsky_TeamBalance;

public class PlayerStats
{
    public string? PlayerName { get; set; }
    public ulong PlayerSteamID { get; set; }
    public int Team { get; set; }
    public int Score { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int Damage { get; set; }

    public float KDA => Deaths == 0 ? (Kills + Assists) : (float)(Kills + Assists) / Deaths;

    public float ConsistencyFactor => (Kills * 0.6f + Damage * 0.4f) / (Kills + Deaths + 1);

    public float PerformanceScore => (Damage * 0.5f) + (KDA * 0.4f) + (ConsistencyFactor * 0.1f);
}
