using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    private readonly BalanceStats balanceStats = new BalanceStats();

    private bool RebalancePlayers(List<PlayerStats> players)
    {
        try
        {
            PrintDebugMessage("Starting player rebalance...");

            balanceStats.GetStats(players);

            var ctPlayerCount = balanceStats.CT.Stats.Count;
            var tPlayerCount = balanceStats.T.Stats.Count;
            var ctTotalScore = balanceStats.CT.TotalPerformanceScore;
            var tTotalScore = balanceStats.T.TotalPerformanceScore;

            PrintDebugMessage($"Current CT Team size: {ctPlayerCount}, T Team size: {tPlayerCount}");
            PrintDebugMessage($"Current CT Score: {ctTotalScore}, T Score: {tTotalScore}");

            // Step 1: Move Phase - Balance team sizes
            if (balanceStats.ShouldMoveLowestScorers())
            {
                balanceStats.MoveLowestScorersFromBiggerTeam();
            }

            // Step 2: Check if scrambling is needed
            if (balanceStats.ShouldScrambleTeams())
            {
                ScrambleTeams();
            }

            // Step 3: Find the best swap (only if not scrambling and using performance score)
            if (Config?.PluginSettings.ScrambleMode == "none" && Config.PluginSettings.UsePerformanceScore)
            {
                var bestSwap = balanceStats.FindBestSwap();
                if (bestSwap != null)
                {
                    balanceStats.PerformSwap(bestSwap.Value.Item1, bestSwap.Value.Item2);
                }
            }

            // Step 4: Apply the team assignments
            balanceStats.AssignPlayerTeams();

            PrintDebugMessage($"Rebalance complete || Final CT Score: {balanceStats.CT.TotalPerformanceScore}, Final T Score: {balanceStats.T.TotalPerformanceScore}, Final CT Players: {balanceStats.CT.Stats.Count}, Final T Players: {balanceStats.T.Stats.Count}");
            return true;
        }
        catch (Exception ex)
        {
            PrintDebugMessage($"Error during rebalancing: {ex.Message}");
            return false;
        }
    }

    private static void ScrambleTeams()
    {
        PrintDebugMessage("Scrambling teams...");

        var players = playerCache.Values.ToList();
        if (players == null || players.Count == 0)
        {
            PrintDebugMessage("No players available for scrambling.");
            return;
        }

        // Shuffle players randomly
        players = [.. players.OrderBy(x => Guid.NewGuid())];

        int halfCount = players.Count / 2;
        int maxTeamSizeDiff = Config?.PluginSettings.MaxTeamSizeDifference ?? 1;

        int ctPlayers = Math.Min(halfCount + maxTeamSizeDiff / 2, players.Count);
        int tPlayers = players.Count - ctPlayers;

        for (int i = 0; i < players.Count; i++)
        {
            int newTeam = (i < ctPlayers) ? (int)CsTeam.CounterTerrorist : (int)CsTeam.Terrorist;
            ChangePlayerTeam(players[i].PlayerSteamID, (CsTeam)newTeam);
        }

        PrintDebugMessage("Teams scrambled successfully.");
    }
}
