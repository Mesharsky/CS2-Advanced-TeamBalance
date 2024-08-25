using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    public bool GlobalBalanceMade = false;

    private void AttemptBalanceTeams()
    {
        PrintDebugMessage("Attempting to balance teams...");

        GlobalBalanceMade = false;

        if (!ShouldTeamsBeRebalanced())
            return;

        PrintDebugMessage("Balancing teams...");

        var players = GetPlayersForRebalance();
        if (players == null || players.Count == 0)
        {
            PrintDebugMessage("No players available for rebalancing.");
            return;
        }

        bool balanceMade = RebalancePlayers(players);
        GlobalBalanceMade = balanceMade;
    }

    private static bool ShouldTeamsBeRebalanced()
    {
        PrintDebugMessage("Evaluating if teams need to be rebalanced...");

        UpdatePlayerTeamsInCache();

        var players = playerCache?.Values.ToList();
        if (players == null || players.Count == 0)
        {
            PrintDebugMessage("No players found for rebalancing.");
            return false;
        }

        int ctPlayerCount = players.Count(p => p.Team == (int)CsTeam.CounterTerrorist);
        int tPlayerCount = players.Count(p => p.Team == (int)CsTeam.Terrorist);

        if (ctPlayerCount + tPlayerCount < (Config?.PluginSettings.MinPlayers ?? 0))
        {
            PrintDebugMessage("Not enough players to balance.");
            return false;
        }

        if (Math.Abs(ctPlayerCount - tPlayerCount) > (Config?.PluginSettings.MaxTeamSizeDifference ?? 1))
        {
            PrintDebugMessage("Team sizes are not equal. Balance needed.");
            return true;
        }

        float ctScore = Config?.PluginSettings.UsePerformanceScore == true
            ? players.Where(p => p.Team == (int)CsTeam.CounterTerrorist).Sum(p => p.PerformanceScore)
            : players.Where(p => p.Team == (int)CsTeam.CounterTerrorist).Sum(p => p.Score);

        float tScore = Config?.PluginSettings.UsePerformanceScore == true
            ? players.Where(p => p.Team == (int)CsTeam.Terrorist).Sum(p => p.PerformanceScore)
            : players.Where(p => p.Team == (int)CsTeam.Terrorist).Sum(p => p.Score);

        if (ctScore > tScore * (Config?.PluginSettings.MaxScoreBalanceRatio ?? 1.0f) || tScore > ctScore * (Config?.PluginSettings.MaxScoreBalanceRatio ?? 1.0f))
        {
            PrintDebugMessage("Score difference is too high. Balance required.");
            return true;
        }

        PrintDebugMessage("No balance required. Teams are balanced.");
        return false;
    }
}
