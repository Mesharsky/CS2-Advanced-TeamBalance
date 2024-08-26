using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    public class BalanceStats
    {
        public TeamStats CT { get; set; } = new TeamStats();
        public TeamStats T { get; set; } = new TeamStats();

        public void GetStats(List<PlayerStats> allPlayers)
        {
            if (allPlayers == null || allPlayers.Count == 0)
            {
                PrintDebugMessage("No players to get stats for.");
                return;
            }

            CT.Reset();
            T.Reset();

            foreach (var player in allPlayers)
            {
                if (player.Team == (int)CsTeam.CounterTerrorist)
                    CT.AddPlayer(player);
                else if (player.Team == (int)CsTeam.Terrorist)
                    T.AddPlayer(player);
            }
        }

        public bool TeamsAreEqualScore()
        {
            return Math.Abs(CT.TotalPerformanceScore - T.TotalPerformanceScore) <= (Config?.PluginSettings.MaxScoreBalanceRatio ?? 2.0f);
        }

        public bool ShouldMoveLowestScorers()
        {
            return Math.Abs(CT.Stats.Count - T.Stats.Count) > (Config?.PluginSettings.MaxTeamSizeDifference ?? 1);
        }

        public void MoveLowestScorersFromBiggerTeam()
        {
            int ctPlayerCount = CT.Stats.Count;
            int tPlayerCount = T.Stats.Count;

            // Determine which team has more players and how many need to be moved
            int playersToMove = Math.Abs(ctPlayerCount - tPlayerCount) / 2;

            if (playersToMove == 0)
            {
                PrintDebugMessage("Teams are already balanced by size. No need to move players.");
                return;
            }

            // Move players from the bigger team to the smaller team
            if (ctPlayerCount > tPlayerCount)
            {
                MovePlayers(CT, T, playersToMove);
            }
            else if (tPlayerCount > ctPlayerCount)
            {
                MovePlayers(T, CT, playersToMove);
            }

            PrintDebugMessage($"Players moved to balance team sizes. CT Players: {CT.Stats.Count}, T Players: {T.Stats.Count}");
        }

        private static void MovePlayers(TeamStats fromTeam, TeamStats toTeam, int playersToMove)
        {
            var playersToMoveList = fromTeam.Stats.OrderBy(p => p.PerformanceScore).Take(playersToMove).ToList();

            foreach (var player in playersToMoveList)
            {
                fromTeam.RemovePlayer(player);
                toTeam.AddPlayer(player);
            }
        }

        public (PlayerStats, PlayerStats)? FindBestSwap()
        {
            float ctScore = CT.TotalPerformanceScore;
            float tScore = T.TotalPerformanceScore;
            float currentDiff = Math.Abs(ctScore - tScore);

            PlayerStats? bestCtPlayer = null;
            PlayerStats? bestTPlayer = T.Stats.OrderBy(p => p.PerformanceScore).FirstOrDefault();
            float bestNewDiff = currentDiff;

            if (bestTPlayer == null || CT.Stats.Count == 0)
            {
                PrintDebugMessage("No valid players for swapping.");
                return null;
            }

            // Iterate over the "winning" team's players to find the best swap
            foreach (var ctPlayer in CT.Stats.OrderByDescending(p => p.PerformanceScore))
            {
                // Calculate the new scores after the swap
                float newCtScore = ctScore - ctPlayer.PerformanceScore + bestTPlayer.PerformanceScore;
                float newTScore = tScore - bestTPlayer.PerformanceScore + ctPlayer.PerformanceScore;

                float biggerScore = Math.Max(newCtScore, newTScore);
                float smallerScore = Math.Min(newCtScore, newTScore);
                float ratio = biggerScore / smallerScore;

                // If this swap improves the balance, store it
                if (ratio <= (Config?.PluginSettings.MaxScoreBalanceRatio ?? 2.0f))
                {
                    bestCtPlayer = ctPlayer;
                    break; // Early exit as the teams are now balanced according to the ratio
                }
                else if (Math.Abs(newCtScore - newTScore) < bestNewDiff)
                {
                    bestNewDiff = Math.Abs(newCtScore - newTScore);
                    bestCtPlayer = ctPlayer;
                }
            }

            // Return the best swap if found
            if (bestCtPlayer != null)
            {
                return (bestCtPlayer, bestTPlayer);
            }

            return null;
        }


        public void PerformSwap(PlayerStats ctPlayer, PlayerStats tPlayer)
        {
            if (ctPlayer == null || tPlayer == null)
            {
                PrintDebugMessage("Cannot swap null players.");
                return;
            }

            CT.RemovePlayer(ctPlayer);
            T.RemovePlayer(tPlayer);

            CT.AddPlayer(tPlayer);
            T.AddPlayer(ctPlayer);

            PrintDebugMessage($"Swapped CT player {ctPlayer.PlayerName} with T player {tPlayer.PlayerName}");
        }

        public void AssignPlayerTeams()
        {
            foreach (var player in CT.Stats)
            {
                if (player == null)
                {
                    PrintDebugMessage("Found null player in CT stats, skipping.");
                    continue;
                }

                if (player.Team != (int)CsTeam.CounterTerrorist)
                    ChangePlayerTeam(player.PlayerSteamID, CsTeam.CounterTerrorist);
            }

            foreach (var player in T.Stats)
            {
                if (player == null)
                {
                    PrintDebugMessage("Found null player in T stats, skipping.");
                    continue;
                }

                if (player.Team != (int)CsTeam.Terrorist)
                    ChangePlayerTeam(player.PlayerSteamID, CsTeam.Terrorist);
            }

            PrintDebugMessage("Player team assignments completed.");
        }
    }
}
