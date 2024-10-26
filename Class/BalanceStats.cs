using CounterStrikeSharp.API.Modules.Utils;
using static Mesharsky_TeamBalance.GameRules;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    public class BalanceStats
    {
        public TeamStats CT { get; set; } = new TeamStats();
        public TeamStats T { get; set; } = new TeamStats();

        public int CTWinStreak { get; private set; } = 0;
        public int TWinStreak { get; private set; } = 0;
        public int RoundCount { get; private set; } = 0;
        public bool WasLastActionScramble { get; private set; } = false;

        public void UpdateStreaks(bool ctWin)
        {
            if (ctWin)
            {
                CTWinStreak++;
                TWinStreak = 0;
            }
            else
            {
                TWinStreak++;
                CTWinStreak = 0;
            }

            RoundCount++;
        }

        public void GetStats(List<PlayerStats> allPlayers)
        {
            if (allPlayers.Count == 0)
            {
                PrintDebugMessage("No players to get stats for.");
                return;
            }

            CT.Reset();
            T.Reset();

            foreach (var player in allPlayers)
            {
                switch (player.Team)
                {
                    case (int)CsTeam.CounterTerrorist:
                        {
                            CT.AddPlayer(player);
                            break;
                        }
                    case (int)CsTeam.Terrorist:
                        {
                            T.AddPlayer(player);
                            break;
                        }
                }
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
                if (player.Team != (int)CsTeam.CounterTerrorist)
                    ChangePlayerTeam(player.PlayerSteamID, CsTeam.CounterTerrorist);
            }

            foreach (var player in T.Stats)
            {
                if (player.Team != (int)CsTeam.Terrorist)
                    ChangePlayerTeam(player.PlayerSteamID, CsTeam.Terrorist);
            }

            PrintDebugMessage("Player team assignments completed.");
        }

        public bool ShouldScrambleTeams()
        {
            if (Config?.PluginSettings.ScrambleMode == "none")
            {
                WasLastActionScramble = false;
                return false;
            }

            if (Config?.PluginSettings.ScrambleMode == "round" && RoundCount >= Config.PluginSettings.RoundScrambleInterval)
            {
                PrintDebugMessage("Scrambling teams due to round interval.");
                RoundCount = 0;
                WasLastActionScramble = true;
                return true;
            }

            if (Config?.PluginSettings.ScrambleMode == "winstreak" && (CTWinStreak >= Config.PluginSettings.WinstreakScrambleThreshold || TWinStreak >= Config.PluginSettings.WinstreakScrambleThreshold))
            {
                PrintDebugMessage("Scrambling teams due to win streak.");
                CTWinStreak = 0;
                TWinStreak = 0;
                WasLastActionScramble = true;
                return true;
            }

            if (Config?.PluginSettings.ScrambleMode == "halftime" && Config.PluginSettings.HalftimeScrambleEnabled && IsHalftime())
            {
                PrintDebugMessage("Scrambling teams due to halftime.");
                WasLastActionScramble = true;
                return true;
            }

            WasLastActionScramble = false;
            return false;
        }
    }
}