namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    private readonly BalanceStats balanceStats = new BalanceStats();

    private bool RebalancePlayers(List<PlayerStats> players)
    {
        PrintDebugMessage("Starting player rebalance...");

        // Collect current stats
        balanceStats.GetStats(players);

        // Pre-checks and debug messages
        var ctPlayerCount = balanceStats.CT.Stats.Count;
        var tPlayerCount = balanceStats.T.Stats.Count;
        var ctTotalScore = balanceStats.CT.TotalPerformanceScore;
        var tTotalScore = balanceStats.T.TotalPerformanceScore;

        PrintDebugMessage($"Current CT Team size: {ctPlayerCount}, T Team size: {tPlayerCount}");
        PrintDebugMessage($"Current CT Score: {ctTotalScore}, T Score: {tTotalScore}");

        if (Math.Abs(ctPlayerCount - tPlayerCount) > Config?.PluginSettings.MaxTeamSizeDifference)
        {
            PrintDebugMessage($"Team size difference exceeds the allowed max_team_size_difference: {Config?.PluginSettings.MaxTeamSizeDifference}. Correction needed.");
        }
        
        if (Math.Abs(ctTotalScore - tTotalScore) > Config?.PluginSettings.MaxScoreBalanceRatio)
        {
            PrintDebugMessage($"Score difference exceeds the allowed score_balance_ratio: {Config?.PluginSettings.MaxScoreBalanceRatio}. Correction needed.");
        }

        // Step 1: Balance team sizes
        if (balanceStats.ShouldMoveLowestScorers())
        {
            balanceStats.MoveLowestScorersFromBiggerTeam();
        }

        // Step 2: Balance teams by performance scores
        if (!balanceStats.TeamsAreEqualScore())
        {
            balanceStats.BalanceTeamsByPerformance();
        }

        // Step 3: Apply the team assignments
        balanceStats.AssignPlayerTeams();

        PrintDebugMessage($"Rebalance complete || Final CT Score: {balanceStats.CT.TotalPerformanceScore}, Final T Score: {balanceStats.T.TotalPerformanceScore}, Final CT Players: {balanceStats.CT.Stats.Count}, Final T Players: {balanceStats.T.Stats.Count}");
        return true;
    }
}
