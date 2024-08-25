namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    private readonly BalanceStats balanceStats = new BalanceStats();

    private bool RebalancePlayers(List<PlayerStats> players)
    {
        PrintDebugMessage("Starting player rebalance...");

        balanceStats.GetStats(players);

        var ctPlayerCount = balanceStats.CT.Stats.Count;
        var tPlayerCount = balanceStats.T.Stats.Count;
        var ctTotalScore = balanceStats.CT.TotalPerformanceScore;
        var tTotalScore = balanceStats.T.TotalPerformanceScore;

        PrintDebugMessage($"Current CT Team size: {ctPlayerCount}, T Team size: {tPlayerCount}");
        PrintDebugMessage($"Current CT Score: {ctTotalScore}, T Score: {tTotalScore}");

        // Step 1: Balance team sizes
        if (balanceStats.ShouldMoveLowestScorers())
        {
            PrintDebugMessage($"Team size difference exceeds the allowed max_team_size_difference: {Config?.PluginSettings.MaxTeamSizeDifference}. Correction needed.");
            balanceStats.MoveLowestScorersFromBiggerTeam();

            // Re-check team sizes after the move
            ctPlayerCount = balanceStats.CT.Stats.Count;
            tPlayerCount = balanceStats.T.Stats.Count;
            PrintDebugMessage($"Post-Move Team Sizes || CT: {ctPlayerCount}, T: {tPlayerCount}");
            
            // Ensure teams are now within the acceptable size difference
            if (Math.Abs(ctPlayerCount - tPlayerCount) > Config?.PluginSettings.MaxTeamSizeDifference)
            {
                PrintDebugMessage($"Failed to balance team sizes adequately. Further intervention needed.");
                return false; // Early exit if team sizes couldn't be balanced
            }
        }

        // Step 2: Balance teams by performance scores
        if (!balanceStats.TeamsAreEqualScore())
        {
            PrintDebugMessage($"Score difference exceeds the allowed score_balance_ratio: {Config?.PluginSettings.MaxScoreBalanceRatio}. Correction needed.");
            balanceStats.BalanceTeamsByPerformance();
            
            // Re-check team scores after balancing by performance
            ctTotalScore = balanceStats.CT.TotalPerformanceScore;
            tTotalScore = balanceStats.T.TotalPerformanceScore;
            PrintDebugMessage($"Post-Performance Balance || CT Score: {ctTotalScore}, T Score: {tTotalScore}");
            
            // Ensure teams are now within the acceptable score balance ratio
            if (!balanceStats.TeamsAreEqualScore())
            {
                PrintDebugMessage($"Failed to balance team performance scores adequately. Further intervention needed.");
                return false; // Early exit if scores couldn't be balanced
            }
        }

        // Step 3: Apply the team assignments
        balanceStats.AssignPlayerTeams();

        PrintDebugMessage($"Rebalance complete || Final CT Score: {balanceStats.CT.TotalPerformanceScore}, Final T Score: {balanceStats.T.TotalPerformanceScore}, Final CT Players: {balanceStats.CT.Stats.Count}, Final T Players: {balanceStats.T.Stats.Count}");
        return true;
    }
}
