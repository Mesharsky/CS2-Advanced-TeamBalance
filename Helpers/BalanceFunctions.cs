using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
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
            Server.PrintToChatAll($" {ChatColors.Red}[Team Balance] {ChatColors.Default}Teams has been balanced");
            //Server.PrintToChatAll($" {ChatColors.Red}[Csowicze] {ChatColors.Default}Drużyny zostały zbalansowane.");
        }
        else
        {
            Server.PrintToChatAll($" {ChatColors.Red}[Team Balance] {ChatColors.Default}No need for team balance at this moment");
            //Server.PrintToChatAll($" {ChatColors.Red}[Csowicze] {ChatColors.Default}System nie wykrył potrzeby balansu drużyn. Brak zmian.");
        }
    }

    private static List<Player> GetPlayersForRebalance()
    {
        var players = playerCache.Values
            .Where(p => p.Team == (int)CsTeam.CounterTerrorist || p.Team == (int)CsTeam.Terrorist)
            .OrderByDescending(p => Config?.PluginSettings.UsePerformanceScore == true ? p.PerformanceScore : p.Score)
            .ToList();

        players.Shuffle();

        PrintDebugMessage($"Total valid players for rebalance: {players.Count}");
        return players;
    }

    // This is ASS, but no other idea at this moment.
    private static bool RebalancePlayers(List<Player> players)
    {
        PrintDebugMessage("Starting player rebalance...");

        int totalPlayers = players.Count;
        int maxPerTeam = totalPlayers / 2 + (totalPlayers % 2);
        int currentRound = GetCurrentRound();

        List<Player> ctTeam = new List<Player>();
        List<Player> tTeam = new List<Player>();
        HashSet<ulong> movedPlayers = new HashSet<ulong>();

        float ctTotalScore = 0f;
        float tTotalScore = 0f;

        bool balanceMade = false;

        PrintDebugMessage($"RebalancePlayers: totalPlayers={totalPlayers}, maxPerTeam={maxPerTeam}");

        // Step 1: Balance team sizes first
        foreach (var player in players)
        {
            bool ctValidChoice = ctTeam.Count < maxPerTeam && ctTeam.Count < tTeam.Count + Config?.PluginSettings.MaxTeamSizeDifference;
            bool tValidChoice = tTeam.Count < maxPerTeam && tTeam.Count < ctTeam.Count + Config?.PluginSettings.MaxTeamSizeDifference;

            if (ctValidChoice && player.Team != (int)CsTeam.CounterTerrorist && CanMovePlayer(ctTeam, tTeam, player, currentRound))
            {
                PrintDebugMessage($"Move {player.PlayerName} to CT (ctTotal={ctTotalScore}, ctCount={ctTeam.Count + 1})");
                ChangePlayerTeam(player.PlayerSteamID, CsTeam.CounterTerrorist);
                ctTeam.Add(player);
                ctTotalScore += player.PerformanceScore;
                movedPlayers.Add(player.PlayerSteamID);
                player.LastMovedRound = currentRound;
                balanceMade = true;
            }
            else if (tValidChoice && player.Team != (int)CsTeam.Terrorist && CanMovePlayer(tTeam, ctTeam, player, currentRound))
            {
                PrintDebugMessage($"Move {player.PlayerName} to T (tTotal={tTotalScore}, tCount={tTeam.Count + 1})");
                ChangePlayerTeam(player.PlayerSteamID, CsTeam.Terrorist);
                tTeam.Add(player);
                tTotalScore += player.PerformanceScore;
                movedPlayers.Add(player.PlayerSteamID);
                player.LastMovedRound = currentRound;
                balanceMade = true;
            }
            else
            {
                // If no moves were made, add the player to their current team
                if (player.Team == (int)CsTeam.CounterTerrorist)
                {
                    ctTeam.Add(player);
                    ctTotalScore += player.PerformanceScore;
                }
                else if (player.Team == (int)CsTeam.Terrorist)
                {
                    tTeam.Add(player);
                    tTotalScore += player.PerformanceScore;
                }
            }
        }

        // Step 2: Further balance by performance score while keeping team sizes in check
        while (Math.Abs(ctTotalScore - tTotalScore) > Config?.PluginSettings.MaxScoreBalanceRatio)
        {
            List<Player> candidatesToMove = ctTotalScore > tTotalScore 
                ? ctTeam.OrderByDescending(p => p.PerformanceScore).ToList()
                : tTeam.OrderByDescending(p => p.PerformanceScore).ToList();

            // Select the first player in the list that can be moved and has not been moved recently
            var playerToMove = candidatesToMove.FirstOrDefault(p => CanMovePlayer(
                ctTotalScore > tTotalScore ? ctTeam : tTeam,
                ctTotalScore > tTotalScore ? tTeam : ctTeam,
                p,
                currentRound
            ));

            if (playerToMove == null) 
                break;

            PrintDebugMessage($"Move {playerToMove.PlayerName} to {(ctTotalScore > tTotalScore ? "T" : "CT")} to balance score.");
            ChangePlayerTeam(playerToMove.PlayerSteamID, ctTotalScore > tTotalScore ? CsTeam.Terrorist : CsTeam.CounterTerrorist);
            ctTeam.Remove(playerToMove);
            tTeam.Add(playerToMove);
            if (ctTotalScore > tTotalScore)
            {
                ctTotalScore -= playerToMove.PerformanceScore;
                tTotalScore += playerToMove.PerformanceScore;
            }
            else
            {
                tTotalScore -= playerToMove.PerformanceScore;
                ctTotalScore += playerToMove.PerformanceScore;
            }

            playerToMove.LastMovedRound = currentRound;
            movedPlayers.Add(playerToMove.PlayerSteamID);
        }

        PrintDebugMessage($"Final Team Distribution - CT: {ctTeam.Count} players, T: {tTeam.Count} players");

        return balanceMade;
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
