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

        List<Player> ctTeam = [];
        List<Player> tTeam = [];

        float ctTotalScore = 0f;
        float tTotalScore = 0f;

        Dictionary<Player, CsTeam> newAssignments = [];

        PrintDebugMessage($"RebalancePlayers: totalPlayers={totalPlayers}, maxPerTeam={maxPerTeam}");

        AssignPlayersToTeams(players, ctTeam, tTeam, newAssignments, ref ctTotalScore, ref tTotalScore, maxPerTeam, currentRound);
        FurtherBalanceTeams(ctTeam, tTeam, newAssignments, ref ctTotalScore, ref tTotalScore, currentRound);

        return ApplyTeamChanges(newAssignments, currentRound);
    }

    private static void AssignPlayersToTeams(List<Player> players, List<Player> ctTeam, List<Player> tTeam, Dictionary<Player, CsTeam> newAssignments, ref float ctTotalScore, ref float tTotalScore, int maxPerTeam, int currentRound)
    {
        foreach (var player in players)
        {
            bool ctValidChoice = ctTeam.Count < maxPerTeam && ctTeam.Count < tTeam.Count + Config?.PluginSettings.MaxTeamSizeDifference;
            bool tValidChoice = tTeam.Count < maxPerTeam && tTeam.Count < ctTeam.Count + Config?.PluginSettings.MaxTeamSizeDifference;

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
            else
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
            }
            else
            {
                newAssignments[playerToMove] = CsTeam.CounterTerrorist;
                tTeam.Remove(playerToMove);
                ctTeam.Add(playerToMove);
                localTTotalScore -= playerToMove.PerformanceScore;
                localCtTotalScore += playerToMove.PerformanceScore;
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
                ChangePlayerTeam(player.PlayerSteamID, newTeam);
                player.LastMovedRound = currentRound;
                balanceMade = true;
            }
        }

        PrintDebugMessage($"Final Team Distribution - CT: {newAssignments.Count(p => p.Value == CsTeam.CounterTerrorist)} players, T: {newAssignments.Count(p => p.Value == CsTeam.Terrorist)} players");

        return balanceMade;
    }
}
