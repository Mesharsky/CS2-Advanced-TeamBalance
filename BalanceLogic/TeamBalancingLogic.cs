using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    private static bool RebalancePlayers(List<Player> players)
    {
        PrintDebugMessage("Starting player rebalance...");

        int totalPlayers = players.Count;
        int maxPerTeam = totalPlayers / 2 + (totalPlayers % 2);
        int currentRound = GetCurrentRound();

        List<Player> ctTeam = new List<Player>();
        List<Player> tTeam = new List<Player>();

        float ctTotalScore = 0f;
        float tTotalScore = 0f;

        Dictionary<Player, CsTeam> newAssignments = new Dictionary<Player, CsTeam>();

        PrintDebugMessage($"RebalancePlayers: totalPlayers={totalPlayers}, maxPerTeam={maxPerTeam}");

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

        // Debug team distribution before balancing
        PrintDebugMessage($"Initial Team Distribution: CT={ctTeam.Count}, T={tTeam.Count}");

        AssignPlayersToTeams(players, ctTeam, tTeam, newAssignments, ref ctTotalScore, ref tTotalScore, maxPerTeam, currentRound);

        // Log the intermediate state after initial assignment
        PrintDebugMessage($"After Initial Assignment: CT Team = {ctTeam.Count}, T Team = {tTeam.Count}");

        FurtherBalanceTeams(ctTeam, tTeam, newAssignments, ref ctTotalScore, ref tTotalScore, currentRound);

        // Log final state before applying changes
        PrintDebugMessage($"After Further Balancing: CT Team = {ctTeam.Count}, T Team = {tTeam.Count}");

        bool balanceMade = ApplyTeamChanges(newAssignments, currentRound);

        // Log the final assignments
        PrintDebugMessage($"Final Team Assignments: {string.Join(", ", newAssignments.Select(kv => $"{kv.Key.PlayerName} -> {kv.Value}"))}");

        return balanceMade;
    }

    private static void AssignPlayersToTeams(List<Player> players, List<Player> ctTeam, List<Player> tTeam, Dictionary<Player, CsTeam> newAssignments, ref float ctTotalScore, ref float tTotalScore, int maxPerTeam, int currentRound)
    {
        foreach (var player in players)
        {
            // Check if the player needs to be reassigned
            if (newAssignments.ContainsKey(player)) continue;

            bool ctValidChoice = ctTeam.Count < maxPerTeam && ctTeam.Count < tTeam.Count + Config?.PluginSettings.MaxTeamSizeDifference;
            bool tValidChoice = tTeam.Count < maxPerTeam && tTeam.Count < ctTeam.Count + Config?.PluginSettings.MaxTeamSizeDifference;

            if (ctValidChoice && player.Team != (int)CsTeam.CounterTerrorist && CanMovePlayer(ctTeam, tTeam, player, currentRound))
            {
                newAssignments[player] = CsTeam.CounterTerrorist;
                ctTeam.Add(player);
                ctTotalScore += player.PerformanceScore;
                PrintDebugMessage($"Assigned {player.PlayerName} to CT Team.");
            }
            else if (tValidChoice && player.Team != (int)CsTeam.Terrorist && CanMovePlayer(tTeam, ctTeam, player, currentRound))
            {
                newAssignments[player] = CsTeam.Terrorist;
                tTeam.Add(player);
                tTotalScore += player.PerformanceScore;
                PrintDebugMessage($"Assigned {player.PlayerName} to T Team.");
            }
        }
    }

    private static void FurtherBalanceTeams(List<Player> ctTeam, List<Player> tTeam, Dictionary<Player, CsTeam> newAssignments, ref float ctTotalScore, ref float tTotalScore, int currentRound)
    {
        float localCtTotalScore = ctTotalScore;
        float localTTotalScore = tTotalScore;

        while (Math.Abs(localCtTotalScore - localTTotalScore) > Config?.PluginSettings.MaxScoreBalanceRatio)
        {
            List<Player> candidatesToMove = localCtTotalScore > localTTotalScore 
                ? ctTeam.OrderByDescending(p => p.PerformanceScore).ToList()
                : tTeam.OrderByDescending(p => p.PerformanceScore).ToList();

            var playerToMove = candidatesToMove.FirstOrDefault(p => CanMovePlayer(
                localCtTotalScore > localTTotalScore ? ctTeam : tTeam,
                localCtTotalScore > localTTotalScore ? tTeam : ctTeam,
                p,
                currentRound
            ));

            if (playerToMove == null)
                break;

            if (localCtTotalScore > localTTotalScore)
            {
                newAssignments[playerToMove] = CsTeam.Terrorist;
                ctTeam.Remove(playerToMove);
                tTeam.Add(playerToMove);
                localCtTotalScore -= playerToMove.PerformanceScore;
                localTTotalScore += playerToMove.PerformanceScore;
                PrintDebugMessage($"Moved {playerToMove.PlayerName} to T Team. New CT Score: {localCtTotalScore}, T Score: {localTTotalScore}");
            }
            else
            {
                newAssignments[playerToMove] = CsTeam.CounterTerrorist;
                tTeam.Remove(playerToMove);
                ctTeam.Add(playerToMove);
                localTTotalScore -= playerToMove.PerformanceScore;
                localCtTotalScore += playerToMove.PerformanceScore;
                PrintDebugMessage($"Moved {playerToMove.PlayerName} to CT Team. New CT Score: {localCtTotalScore}, T Score: {localTTotalScore}");
            }
        }

        ctTotalScore = localCtTotalScore;
        tTotalScore = localTTotalScore;
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
                    PrintDebugMessage($"Player {player.PlayerName} successfully moved to team {newTeam}.");
                }
                else
                {
                    PrintDebugMessage($"Failed to move player {player.PlayerName} to team {newTeam}.");
                }
            }
        }

        PrintDebugMessage($"Final Team Distribution - CT: {newAssignments.Count(p => p.Value == CsTeam.CounterTerrorist)} players, T: {newAssignments.Count(p => p.Value == CsTeam.Terrorist)} players");

        return balanceMade;
    }
}
