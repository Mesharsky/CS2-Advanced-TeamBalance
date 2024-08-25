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
            while (CT.Stats.Count != T.Stats.Count)
            {
                if (CT.Stats.Count > T.Stats.Count)
                {
                    var playerToMove = CT.Stats.OrderBy(p => p.PerformanceScore).FirstOrDefault();
                    if (playerToMove != null)
                    {
                        T.AddPlayer(playerToMove);
                        CT.RemovePlayer(playerToMove);
                    }
                }
                else if (T.Stats.Count > CT.Stats.Count)
                {
                    var playerToMove = T.Stats.OrderBy(p => p.PerformanceScore).FirstOrDefault();
                    if (playerToMove != null)
                    {
                        CT.AddPlayer(playerToMove);
                        T.RemovePlayer(playerToMove);
                    }
                }
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

            if (bestTPlayer == null)
            {
                return null;
            }

            // Iterate over the "winning" team's players to find the best swap
            foreach (var ctPlayer in CT.Stats.OrderByDescending(p => p.PerformanceScore))
            {
                // Calculate the new difference after the swap
                float newCtScore = ctScore - ctPlayer.PerformanceScore + bestTPlayer.PerformanceScore;
                float newTScore = tScore - bestTPlayer.PerformanceScore + ctPlayer.PerformanceScore;
                float newDiff = Math.Abs(newCtScore - newTScore);

                // If this swap improves the balance, store it
                if (newDiff < bestNewDiff)
                {
                    bestNewDiff = newDiff;
                    bestCtPlayer = ctPlayer;

                    // Exit if the swap fucks up balance ratio
                    if (newDiff <= (Config?.PluginSettings.MaxScoreBalanceRatio ?? 1.0f))
                    {
                        break;
                    }
                }
            }

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
        }

        public void AssignPlayerTeams()
        {
            foreach (var player in CT.Stats)
            {
                if (player.Team != (int)CsTeam.CounterTerrorist)
                    ChangePlayerTeam(player.PlayerSteamID, CsTeam.CounterTerrorist);
            }

            foreach (var player in T.Stats)
            {
                if (player.Team != (int)CsTeam.Terrorist)
                    ChangePlayerTeam(player.PlayerSteamID, CsTeam.Terrorist);
            }
        }
    }
}
