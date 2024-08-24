using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    private static bool RebalancePlayers(List<Player> players)
    {
        PrintDebugMessage("Starting player rebalance...");

        var currentRound = GetCurrentRound();

        var ctTeam = players.Where(p => p.Team == (int)CsTeam.CounterTerrorist).ToList();
        var tTeam = players.Where(p => p.Team == (int)CsTeam.Terrorist).ToList();

        var ctTotalScore = ctTeam.Sum(p => p.PerformanceScore);
        var tTotalScore = tTeam.Sum(p => p.PerformanceScore);

        PrintDebugMessage($"Initial CT Score: {ctTotalScore}, T Score: {tTotalScore}");

        // Step 1: Ensure the teams are within MaxTeamSizeDifference by MOVING players
        while (Math.Abs(ctTeam.Count - tTeam.Count) > Config?.PluginSettings.MaxTeamSizeDifference)
        {
            var difference = ctTeam.Count - tTeam.Count;

            if (difference > 0)
            {
                // More CTs, move one to T
                var playerToMove = ctTeam.OrderByDescending(p => p.PerformanceScore).First();
                MovePlayer(playerToMove, tTeam, ctTeam, ref ctTotalScore, ref tTotalScore);
            }
            else
            {
                // More Ts, move one to CT
                var playerToMove = tTeam.OrderByDescending(p => p.PerformanceScore).First();
                MovePlayer(playerToMove, ctTeam, tTeam, ref tTotalScore, ref ctTotalScore);
            }
        }

        // Step 2: Balance teams by TRADING players to minimize performance score differences
        SwapPlayersToBalance(ctTeam, tTeam, ref ctTotalScore, ref tTotalScore, currentRound);

        // Step 3: Apply changes if any balancing was performed
        bool balanceMade = ApplyTeamChanges(ctTeam, tTeam, currentRound);

        return balanceMade;
    }

    // This method is responsible for swapping players between teams to balance performance scores
    private static void SwapPlayersToBalance(List<Player> ctTeam, List<Player> tTeam, ref float ctTotalScore, ref float tTotalScore, int currentRound)
    {
        while (Math.Abs(ctTotalScore - tTotalScore) > Config?.PluginSettings.MaxScoreBalanceRatio)
        {
            var difference = Math.Abs(ctTotalScore - tTotalScore);

            var bestCtPlayerToMove = FindBestPlayerToMove(ctTeam, tTeam, ctTotalScore, tTotalScore, currentRound, difference);
            var bestTPlayerToMove = FindBestPlayerToMove(tTeam, ctTeam, tTotalScore, ctTotalScore, currentRound, difference);

            if (bestCtPlayerToMove == null && bestTPlayerToMove == null)
                break;

            // Perform the trade that best reduces the difference, respecting the new difference
            if (bestCtPlayerToMove.HasValue && bestTPlayerToMove.HasValue)
            {
                TradePlayers(bestCtPlayerToMove.Value.Item1, bestTPlayerToMove.Value.Item1, ctTeam, tTeam, ref ctTotalScore, ref tTotalScore);
            }

            // Update the difference after the trade
            difference = Math.Abs(ctTotalScore - tTotalScore);

            // Break out of the loop if no further meaningful trades can be made
            if (bestCtPlayerToMove.HasValue && bestCtPlayerToMove.Value.Item1.PerformanceScore > difference &&
                bestTPlayerToMove.HasValue && bestTPlayerToMove.Value.Item1.PerformanceScore > difference)
            {
                break;
            }
        }
    }

    // This method is responsible for finding the best player to move based on minimizing the performance difference
    private static (Player, float)? FindBestPlayerToMove(List<Player> fromTeam, List<Player> toTeam, float fromTeamScore, float toTeamScore, int currentRound, float difference)
    {
        return fromTeam
            .Where(p => CanMovePlayer(fromTeam, toTeam, p, currentRound) && p.PerformanceScore <= difference)
            .Select(p => (p, Math.Abs(fromTeamScore - p.PerformanceScore - (toTeamScore + p.PerformanceScore))))
            .OrderBy(result => result.Item2)
            .FirstOrDefault();
    }

    private static void TradePlayers(Player ctPlayer, Player tPlayer, List<Player> ctTeam, List<Player> tTeam, ref float ctTotalScore, ref float tTotalScore)
    {
        // Remove players from their current teams
        ctTeam.Remove(ctPlayer);
        tTeam.Remove(tPlayer);

        // Swap the players
        ctTeam.Add(tPlayer);
        tTeam.Add(ctPlayer);

        // Adjust the total scores accordingly
        ctTotalScore = ctTotalScore - ctPlayer.PerformanceScore + tPlayer.PerformanceScore;
        tTotalScore = tTotalScore - tPlayer.PerformanceScore + ctPlayer.PerformanceScore;

        PrintDebugMessage($"Traded {ctPlayer.PlayerName} with {tPlayer.PlayerName}. New scores: CT = {ctTotalScore}, T = {tTotalScore}");
    }

    private static void MovePlayer(Player player, List<Player> toTeam, List<Player> fromTeam, ref float fromTeamScore, ref float toTeamScore)
    {
        fromTeam.Remove(player);
        toTeam.Add(player);

        fromTeamScore -= player.PerformanceScore;
        toTeamScore += player.PerformanceScore;

        PrintDebugMessage($"Moved {player.PlayerName} to the opposite team. New scores: CT = {fromTeamScore}, T = {toTeamScore}");
    }
    
    private static bool ApplyTeamChanges(List<Player> ctTeam, List<Player> tTeam, int currentRound)
    {
        bool balanceMade = false;

        foreach (var player in ctTeam)
        {
            if (player.Team != (int)CsTeam.CounterTerrorist)
            {
                ChangePlayerTeam(player.PlayerSteamID, CsTeam.CounterTerrorist);
                player.LastMovedRound = currentRound;
                balanceMade = true;
            }
        }

        foreach (var player in tTeam)
        {
            if (player.Team != (int)CsTeam.Terrorist)
            {
                ChangePlayerTeam(player.PlayerSteamID, CsTeam.Terrorist);
                player.LastMovedRound = currentRound;
                balanceMade = true;
            }
        }

        return balanceMade;
    }
}