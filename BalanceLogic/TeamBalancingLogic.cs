using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    private static bool RebalancePlayers(List<Player> players)
    {
        int totalPlayers = players.Count;
        int maxPerTeam = totalPlayers / 2 + (totalPlayers % 2);
        int currentRound = GetCurrentRound();

        List<Player> ctTeam = new List<Player>();
        List<Player> tTeam = new List<Player>();

        float ctTotalScore = 0f;
        float tTotalScore = 0f;

        Dictionary<Player, CsTeam> newAssignments = new Dictionary<Player, CsTeam>();

        // Initialize teams based on current player assignments
        foreach (var player in players)
        {
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

        // Assign players to teams
        foreach (var player in players)
        {
            if (newAssignments.ContainsKey(player)) continue;

            bool ctValidChoice = ctTeam.Count < maxPerTeam;
            bool tValidChoice = tTeam.Count < maxPerTeam;

            if (ctValidChoice && player.Team != (int)CsTeam.CounterTerrorist && CanMovePlayer(ctTeam, tTeam, player, currentRound))
            {
                newAssignments[player] = CsTeam.CounterTerrorist;
                ctTeam.Add(player);
                ctTotalScore += player.PerformanceScore;
            }
            else if (tValidChoice && player.Team != (int)CsTeam.Terrorist && CanMovePlayer(tTeam, ctTeam, player, currentRound))
            {
                newAssignments[player] = CsTeam.Terrorist;
                tTeam.Add(player);
                tTotalScore += player.PerformanceScore;
            }
        }

        // Further balance by score
        BalanceByScore(ctTeam, tTeam, newAssignments, currentRound);

        // Apply changes
        return ApplyTeamChanges(newAssignments, currentRound);
    }

    private static void BalanceByScore(List<Player> ctTeam, List<Player> tTeam, Dictionary<Player, CsTeam> newAssignments, int currentRound)
    {
        float ctTotalScore = ctTeam.Sum(p => p.PerformanceScore);
        float tTotalScore = tTeam.Sum(p => p.PerformanceScore);

        while (Math.Abs(ctTotalScore - tTotalScore) > Config?.PluginSettings.MaxScoreBalanceRatio)
        {
            List<Player> candidatesToMove = ctTotalScore > tTotalScore 
                ? ctTeam.OrderByDescending(p => p.PerformanceScore).ToList()
                : tTeam.OrderByDescending(p => p.PerformanceScore).ToList();

            var playerToMove = candidatesToMove.FirstOrDefault(p => CanMovePlayer(
                ctTotalScore > tTotalScore ? ctTeam : tTeam,
                ctTotalScore > tTotalScore ? tTeam : ctTeam,
                p,
                currentRound
            ));

            if (playerToMove == null)
            {
                PrintDebugMessage("No eligible player found to move during further balancing.");
                break;
            }

            if (ctTotalScore > tTotalScore)
            {
                newAssignments[playerToMove] = CsTeam.Terrorist;
                ctTeam.Remove(playerToMove);
                tTeam.Add(playerToMove);
                ctTotalScore -= playerToMove.PerformanceScore;
                tTotalScore += playerToMove.PerformanceScore;
                PrintDebugMessage($"Moved {playerToMove.PlayerName} to T Team. New CT Score: {ctTotalScore}, T Score: {tTotalScore}");
            }
            else
            {
                newAssignments[playerToMove] = CsTeam.CounterTerrorist;
                tTeam.Remove(playerToMove);
                ctTeam.Add(playerToMove);
                tTotalScore -= playerToMove.PerformanceScore;
                ctTotalScore += playerToMove.PerformanceScore;
                PrintDebugMessage($"Moved {playerToMove.PlayerName} to CT Team. New CT Score: {ctTotalScore}, T Score: {tTotalScore}");
            }

            // Break the loop if no significant change is achieved after a move
            if (Math.Abs(ctTotalScore - tTotalScore) < Config?.PluginSettings.MaxScoreBalanceRatio)
            {
                break;
            }
        }
    }

    private static bool ApplyTeamChanges(Dictionary<Player, CsTeam> newAssignments, int currentRound)
    {
        bool balanceMade = false;
        foreach (var assignment in newAssignments)
        {
            var player = assignment.Key;
            var newTeam = assignment.Value;

            if (player.Team != (int)newTeam)
            {
                if (ChangePlayerTeam(player.PlayerSteamID, newTeam))
                {
                    player.LastMovedRound = currentRound;
                    balanceMade = true;
                }
            }
        }

        return balanceMade;
    }

}
