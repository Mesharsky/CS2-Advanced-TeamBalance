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

        PrintDebugMessage($"Initial CT Score: {ctTotalScore}, T Score: {tTotalScore}, CT Players: {ctTeam.Count}, T Players: {tTeam.Count}");

        // Determine balance trigger reasons
        bool sizeDifferenceTriggered = Math.Abs(ctTeam.Count - tTeam.Count) > Config?.PluginSettings.MaxTeamSizeDifference;
        bool scoreDifferenceTriggered = Math.Abs(ctTotalScore - tTotalScore) > (tTotalScore * Config?.PluginSettings.MaxScoreBalanceRatio);

        PrintDebugMessage($"Balance Triggered by Size Difference: {sizeDifferenceTriggered}, Score Difference: {scoreDifferenceTriggered}");

        // Step 1: Ensure the teams are within MaxTeamSizeDifference by MOVING players
        int attempts = 0;
        while (sizeDifferenceTriggered && Math.Abs(ctTeam.Count - tTeam.Count) > Config?.PluginSettings.MaxTeamSizeDifference)
        {
            attempts++;
            var difference = ctTeam.Count - tTeam.Count;

            if (difference > 0)
            {
                var playerToMove = ctTeam.OrderByDescending(p => p.PerformanceScore).First();
                MovePlayer(playerToMove, tTeam, ctTeam, ref ctTotalScore, ref tTotalScore);
            }
            else
            {
                var playerToMove = tTeam.OrderByDescending(p => p.PerformanceScore).First();
                MovePlayer(playerToMove, ctTeam, tTeam, ref tTotalScore, ref ctTotalScore);
            }

            if (attempts >= 10)
            {
                PrintDebugMessage("Maximum move attempts reached. Exiting to prevent infinite loop.");
                break;
            }
        }

        // Step 2: Balance teams by TRADING players to minimize performance score differences
        if (scoreDifferenceTriggered || !sizeDifferenceTriggered)
        {
            SwapPlayersToBalance(ctTeam, tTeam, ref ctTotalScore, ref tTotalScore, currentRound);
        }

        // Step 3: Apply changes if any balancing was performed
        bool balanceMade = ApplyTeamChanges(ctTeam, tTeam, currentRound);

        PrintDebugMessage($"Rebalance complete || Final CT Score: {ctTotalScore}, Final T Score: {tTotalScore}, Final CT Players: {ctTeam.Count}, Final T Players: {tTeam.Count}");
        return balanceMade;
    }

    private static void SwapPlayersToBalance(List<Player> ctTeam, List<Player> tTeam, ref float ctTotalScore, ref float tTotalScore, int currentRound)
    {
        int maxAttempts = 10;
        int attempts = 0;

        while (Math.Abs(ctTotalScore - tTotalScore) > Config?.PluginSettings.MaxScoreBalanceRatio && attempts < maxAttempts)
        {
            attempts++;

            var difference = Math.Abs(ctTotalScore - tTotalScore);
            PrintDebugMessage($"Attempt {attempts}: Current Score Difference = {difference}, Max Allowed = {tTotalScore * Config?.PluginSettings.MaxScoreBalanceRatio}");

            // Find the best players to swap
            var bestCtPlayerToMove = FindBestPlayerToMove(ctTeam, tTeam, ctTotalScore, tTotalScore, currentRound, difference);
            var bestTPlayerToMove = FindBestPlayerToMove(tTeam, ctTeam, tTotalScore, ctTotalScore, currentRound, difference);

            // If both players are found, proceed with the trade
            if (bestCtPlayerToMove.HasValue && bestTPlayerToMove.HasValue)
            {
                var ctPlayer = bestCtPlayerToMove.Value.Item1;
                var tPlayer = bestTPlayerToMove.Value.Item1;

                // Ensure both players are valid before proceeding
                if (ctPlayer != null && tPlayer != null)
                {
                    TradePlayers(ctPlayer, tPlayer, ctTeam, tTeam, ref ctTotalScore, ref tTotalScore);
                }
                else
                {
                    PrintDebugMessage("One of the players is null. Skipping trade.");
                    break;
                }
            }
            else
            {
                // If no valid players are found for trading, exit the loop
                PrintDebugMessage("No valid players found for trading. Exiting swap loop.");
                break;
            }

            // Safety check: Break out of the loop if no further meaningful trades can be made
            if (attempts > 1 && Math.Abs(ctTotalScore - tTotalScore) == difference)
            {
                PrintDebugMessage("Score difference unchanged after trade attempt. Exiting swap loop.");
                break;
            }
        }

        if (attempts >= maxAttempts)
        {
            PrintDebugMessage("Maximum attempts reached. Exiting swap loop to prevent infinite loop.");
        }
    }


    // Find the best player to move based on minimizing the performance difference
    private static (Player, float)? FindBestPlayerToMove(List<Player> fromTeam, List<Player> toTeam, float fromTeamScore, float toTeamScore, int currentRound, float difference)
    {
        var potentialPlayers = fromTeam
            .Where(p => p != null && CanMovePlayer(fromTeam, toTeam, p, currentRound) && p.PerformanceScore <= difference)
            .Select(p => (p, Math.Abs(fromTeamScore - p.PerformanceScore - (toTeamScore + p.PerformanceScore))))
            .OrderBy(result => result.Item2)
            .ToList();

        // Debug logging to understand why no valid players are found
        if (potentialPlayers.Count == 0)
        {
            PrintDebugMessage("No players eligible for moving under current conditions.");
        }
        else
        {
            PrintDebugMessage($"Found {potentialPlayers.Count} potential players for trading. Top candidate: {potentialPlayers.First().Item1.PlayerName} with score difference: {potentialPlayers.First().Item2}");
        }

        return potentialPlayers.FirstOrDefault();
    }

    private static void TradePlayers(Player ctPlayer, Player tPlayer, List<Player> ctTeam, List<Player> tTeam, ref float ctTotalScore, ref float tTotalScore)
    {
        // Validate that both players are still on their respective teams
        if (!ctTeam.Contains(ctPlayer) || !tTeam.Contains(tPlayer))
        {
            PrintDebugMessage("Player no longer in the original team. Skipping trade.");
            return;
        }

        // Safeguard: Ensure trade will reduce the score imbalance
        float newCtTotalScore = ctTotalScore - ctPlayer.PerformanceScore + tPlayer.PerformanceScore;
        float newTTotalScore = tTotalScore - tPlayer.PerformanceScore + ctPlayer.PerformanceScore;

        if (Math.Abs(newCtTotalScore - newTTotalScore) >= Math.Abs(ctTotalScore - tTotalScore))
        {
            PrintDebugMessage("Trade would worsen score imbalance. Skipping trade.");
            return;
        }

        // Perform the trade
        ctTeam.Remove(ctPlayer);
        tTeam.Remove(tPlayer);

        ctTeam.Add(tPlayer);
        tTeam.Add(ctPlayer);

        // Adjust the team scores
        ctTotalScore = newCtTotalScore;
        tTotalScore = newTTotalScore;

        PrintDebugMessage($"Traded {ctPlayer.PlayerName} with {tPlayer.PlayerName}. New scores: CT = {ctTotalScore}, T = {tTotalScore}");
    }

    private static void MovePlayer(Player player, List<Player> toTeam, List<Player> fromTeam, ref float fromTeamScore, ref float toTeamScore, bool forceMove = false)
    {
        if (player == null || fromTeam == null || toTeam == null)
        {
            PrintDebugMessage("Invalid operation. Either player or team is null.");
            return;
        }

        // Safeguard: Do not move if the move would make score differences worse unless forceMove is true
        float projectedFromTeamScore = fromTeamScore - player.PerformanceScore;
        float projectedToTeamScore = toTeamScore + player.PerformanceScore;

        if (Math.Abs(projectedFromTeamScore - projectedToTeamScore) > Math.Abs(fromTeamScore - toTeamScore) && !forceMove)
        {
            PrintDebugMessage("Move would worsen score imbalance. Skipping move.");
            return;
        }

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
                balanceMade = true;
            }
        }

        foreach (var player in tTeam)
        {
            if (player.Team != (int)CsTeam.Terrorist)
            {
                ChangePlayerTeam(player.PlayerSteamID, CsTeam.Terrorist);
                balanceMade = true;
            }
        }

        return balanceMade;
    }
}