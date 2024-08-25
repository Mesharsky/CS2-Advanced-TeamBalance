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
            return Math.Abs(CT.TotalPerformanceScore - T.TotalPerformanceScore) <= (Config?.PluginSettings.MaxScoreBalanceRatio ?? 1.0f);
        }

        public bool ShouldMoveLowestScorers()
        {
            return Math.Abs(CT.Stats.Count - T.Stats.Count) > (Config?.PluginSettings.MaxTeamSizeDifference ?? 1);
        }

        public void MoveLowestScorersFromBiggerTeam()
        {
            int maxAttempts = 20;
            int attempts = 0;

            while (CT.Stats.Count != T.Stats.Count && attempts < maxAttempts)
            {
                attempts++;

                if (CT.Stats.Count > T.Stats.Count)
                {
                    var playerToMove = CT.Stats.OrderBy(p => p.PerformanceScore).FirstOrDefault();
                    if (playerToMove != null)
                    {
                        T.AddPlayer(playerToMove);
                        CT.RemovePlayer(playerToMove);
                        PrintDebugMessage($"Moved player {playerToMove.PlayerName} from CT to T.");
                    }
                }
                else if (T.Stats.Count > CT.Stats.Count)
                {
                    var playerToMove = T.Stats.OrderBy(p => p.PerformanceScore).FirstOrDefault();
                    if (playerToMove != null)
                    {
                        CT.AddPlayer(playerToMove);
                        T.RemovePlayer(playerToMove);
                        PrintDebugMessage($"Moved player {playerToMove.PlayerName} from T to CT.");
                    }
                }

                // After each move, re-check the balance to ensure no infinite loop
                if (Math.Abs(CT.Stats.Count - T.Stats.Count) <= Config?.PluginSettings.MaxTeamSizeDifference)
                {
                    break; // Exit early if the size difference is now acceptable
                }
            }

            if (attempts >= maxAttempts)
            {
                PrintDebugMessage("Maximum attempts reached while balancing team sizes. Exiting to prevent infinite loop.");
            }
        }

        public void BalanceTeamsByPerformance()
        {
            int maxAttempts = 30;
            int attempts = 0;

            while (!TeamsAreEqualScore() && attempts < maxAttempts)
            {
                attempts++;

                var ctPlayer = CT.Stats.OrderByDescending(p => p.PerformanceScore).FirstOrDefault();
                var tPlayer = T.Stats.OrderByDescending(p => p.PerformanceScore).FirstOrDefault();

                if (ctPlayer != null && tPlayer != null)
                {
                    SwapPlayers(ctPlayer, tPlayer);
                    PrintDebugMessage($"Swapped player {ctPlayer.PlayerName} with {tPlayer.PlayerName}");
                    
                }
                else
                {
                    PrintDebugMessage("No valid players found for swapping. Exiting loop.");
                    break;
                }

                if (attempts >= maxAttempts)
                {
                    PrintDebugMessage("Maximum attempts reached while balancing team performance. Exiting to prevent infinite loop.");
                    break;
                }
            }
        }

        private void SwapPlayers(PlayerStats ctPlayer, PlayerStats tPlayer)
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
