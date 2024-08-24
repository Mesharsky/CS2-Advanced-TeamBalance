using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    public bool GlobalBalanceMade = false;
    private void AttemptBalanceTeams()
    {
        PrintDebugMessage("Attempting to balance teams...");

        if (!ShouldTeamsBeRebalanced())
            return;

        PrintDebugMessage("Balancing teams...");

        var players = GetPlayersForRebalance();
        bool balanceMade = RebalancePlayers(players);

        if (balanceMade)
        {
            GlobalBalanceMade = true;
        }
        else
        {
            GlobalBalanceMade = false;
        }
    }

    private static bool ShouldTeamsBeRebalanced()
    {
        PrintDebugMessage("Evaluating if teams need to be rebalanced...");

        UpdatePlayerTeamsInCache();

        var players = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected)
            .ToList();

        int ctPlayerCount = players.Count(p => p.Team == CsTeam.CounterTerrorist);
        int tPlayerCount = players.Count(p => p.Team == CsTeam.Terrorist);

        if (ctPlayerCount + tPlayerCount < Config?.PluginSettings.MinPlayers)
        {
            PrintDebugMessage("Not enough players to balance.");
            return false;
        }

        int ctScore = players.Where(p => p.Team == CsTeam.CounterTerrorist).Sum(p => p.Score);
        int tScore = players.Where(p => p.Team == CsTeam.Terrorist).Sum(p => p.Score);

        if (ctScore > tScore * Config?.PluginSettings.MaxScoreBalanceRatio || tScore > ctScore * Config?.PluginSettings.MaxScoreBalanceRatio)
        {
            PrintDebugMessage("Score difference is too high. Balance required.");
            return true;
        }

        if (Math.Abs(ctPlayerCount - tPlayerCount) > Config?.PluginSettings.MaxTeamSizeDifference)
        {
            PrintDebugMessage("Team sizes are not equal. Balance needed.");
            return true;
        }

        PrintDebugMessage("No balance required. Teams are balanced.");
        return false;
    }
}
