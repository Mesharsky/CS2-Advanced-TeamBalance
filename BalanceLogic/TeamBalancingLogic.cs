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

        // Step 1: Move Phase - Balance team sizes
        if (balanceStats.ShouldMoveLowestScorers())
        {
            balanceStats.MoveLowestScorersFromBiggerTeam();
        }

        // Step 2: Balancing Phase - Find the best swap
        var bestSwap = balanceStats.FindBestSwap();
        if (bestSwap != null)
        {
            balanceStats.PerformSwap(bestSwap.Value.Item1, bestSwap.Value.Item2);
        }

        // Step 3: Apply the team assignments
        balanceStats.AssignPlayerTeams();

        PrintDebugMessage($"Rebalance complete || Final CT Score: {balanceStats.CT.TotalPerformanceScore}, Final T Score: {balanceStats.T.TotalPerformanceScore}, Final CT Players: {balanceStats.CT.Stats.Count}, Final T Players: {balanceStats.T.Stats.Count}");
        return true;
    }
}
