using System;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace AdvancedTeamBalance
{
    /// <summary>
    /// Manages game events related to team balancing
    /// </summary>
    public static class EventManager
    {
        private static PluginConfig _config = null!;
        private static CCSGameRulesProxy? _gameRulesEntity = null;
        private static int _tWinStreak = 0;
        private static int _ctWinStreak = 0;

        public static void Initialize(PluginConfig config)
        {
            _config = config;
        }
        
        public static HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid || player.IsBot)
                return HookResult.Continue;
                
            var playerData = PlayerManager.GetOrAddPlayer(player);
            
            playerData.IsAlive = player.PawnIsAlive;
            
            if (_config.TeamSwitch.BalanceTriggers.Contains("OnPlayerJoin") && HasEnoughPlayers())
            {
                PerformTeamBalancing();
            }
            
            return HookResult.Continue;
        }

        public static HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid || player.IsBot)
                return HookResult.Continue;
            
            PlayerManager.MarkPlayerDisconnected(player.SteamID);
            
            if (_config.TeamSwitch.BalanceTriggers.Contains("OnPlayerDisconnect") && HasEnoughPlayers())
            {
                PerformTeamBalancing();
            }
            
            return HookResult.Continue;
        }

        public static HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            var victim = @event.Userid;
            var attacker = @event.Attacker;
            var assister = @event.Assister;
            
            if (victim != null && victim.IsValid && !victim.IsBot)
            {
                var victimPlayer = PlayerManager.GetOrAddPlayer(victim);
                victimPlayer.Stats.Deaths++;
                victimPlayer.IsAlive = false;
            }
            
            if (attacker != null && attacker.IsValid && !attacker.IsBot)
            {
                var attackerPlayer = PlayerManager.GetOrAddPlayer(attacker);
                attackerPlayer.Stats.Kills++;
            }
            
            if (assister != null && assister.IsValid && !assister.IsBot)
            {
                var assisterPlayer = PlayerManager.GetOrAddPlayer(assister);
                assisterPlayer.Stats.Assists++;
            }
            
            return HookResult.Continue;
        }
        
        public static HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var player = @event.Userid;
            if (player == null || !player.IsValid || player.IsBot)
                return HookResult.Continue;
                
            var playerData = PlayerManager.GetOrAddPlayer(player);
            playerData.IsAlive = true; // Mark player as alive
            
            return HookResult.Continue;
        }
        
        public static HookResult OnRoundStart(EventRoundPrestart @event, GameEventInfo info)
        {
            PlayerManager.VerifyAllPlayers();
            PlayerManager.SyncPlayerData();
            UpdatePlayerRoundCounts();
            
            if (_config.TeamSwitch.BalanceTriggers.Contains("OnRoundStart") && 
                (!IsWarmup() || _config.TeamSwitch.BalanceDuringWarmup) && 
                HasEnoughPlayers())
            {
                PerformTeamBalancing();
            }
            
            foreach (var player in PlayerManager.GetAllPlayers())
            {
                player.Stats.RoundsPlayed++;
            }
            
            return HookResult.Continue;
        }

        public static HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            if (IsWarmup())
                return HookResult.Continue;
                
            var winnerTeam = @event.Winner;
            
            // Update win streaks
            if (winnerTeam == (int)CsTeam.Terrorist)
            {
                _tWinStreak++;
                _ctWinStreak = 0;
                
                // Give win credit to T players
                foreach (var player in PlayerManager.GetPlayersByTeam(CsTeam.Terrorist))
                {
                    player.Stats.RoundsWon++;
                }
                
                if (_config.General.EnableDebug)
                {
                    Console.WriteLine($"[AdvancedTeamBalance] T team won, win streak: {_tWinStreak}, CT lose streak: {_tWinStreak}");
                }
            }
            else if (winnerTeam == (int)CsTeam.CounterTerrorist)
            {
                _ctWinStreak++;
                _tWinStreak = 0;
                
                foreach (var player in PlayerManager.GetPlayersByTeam(CsTeam.CounterTerrorist))
                {
                    player.Stats.RoundsWon++;
                }
                
                if (_config.General.EnableDebug)
                {
                    Console.WriteLine($"[AdvancedTeamBalance] CT team won, win streak: {_ctWinStreak}, T lose streak: {_ctWinStreak}");
                }
            }
            
            // Check for auto-scramble condition
            if (_config.Balancing.AutoScrambleAfterWinStreak > 0)
            {
                if (_tWinStreak >= _config.Balancing.AutoScrambleAfterWinStreak || 
                    _ctWinStreak >= _config.Balancing.AutoScrambleAfterWinStreak)
                {
                    string winningTeam = _tWinStreak > _ctWinStreak ? "Terrorists" : "Counter-Terrorists";
                    int streak = _tWinStreak > _ctWinStreak ? _tWinStreak : _ctWinStreak;
                    
                    if (_config.Messages.AnnounceBalancing)
                    {
                        ChatHelper.PrintLocalizedChatAll(true, "balance.scramble.winstreak", winningTeam, streak);
                    }
                    
                    PerformTeamScramble();
                    
                    _tWinStreak = 0;
                    _ctWinStreak = 0;
                }
            }
            
            // Check for boost condition and announce if configured (but only if not OnlyBalanceByTeamSize)
            if (_config.Balancing.BoostAfterLoseStreak > 0 && !_config.Balancing.OnlyBalanceByTeamSize)
            {
                string? losingTeam = null;
                int loseStreak = 0;
                
                if (_tWinStreak >= _config.Balancing.BoostAfterLoseStreak)
                {
                    // CT team is on a losing streak
                    losingTeam = "Counter-Terrorists";
                    loseStreak = _tWinStreak;
                }
                else if (_ctWinStreak >= _config.Balancing.BoostAfterLoseStreak)
                {
                    // T team is on a losing streak
                    losingTeam = "Terrorists";
                    loseStreak = _ctWinStreak;
                }
                
                if (losingTeam != null && _config.Messages.AnnounceBalancing && _config.Messages.ExplainBalanceReason)
                {
                    ChatHelper.PrintLocalizedChatAll(true, "balance.boost.losestreak", losingTeam, loseStreak);
                }
            }
            
            if (_config.TeamSwitch.BalanceTriggers.Contains("OnRoundEnd") && HasEnoughPlayers())
            {
                PerformTeamBalancing();
            }
            
            return HookResult.Continue;
        }
        
        /// <summary>
        /// Check if the server has enough players for balancing
        /// </summary>
        private static bool HasEnoughPlayers()
        {
            var total = PlayerManager.GetAllPlayers().Count;
            return total >= _config.General.MinimumPlayers;
        }
        
        /// <summary>
        /// Update player round counts
        /// </summary>
        private static void UpdatePlayerRoundCounts()
        {
            foreach (var player in PlayerManager.GetAllPlayers())
            {
                // Update immunity time
                if (player.ImmunityTimeRemaining > 0)
                {
                    player.ImmunityTimeRemaining--;
                }
                
                player.RoundsOnCurrentTeam++;
            }
        }
        
        /// <summary>
        /// Perform the appropriate team balancing based on configuration
        /// </summary>
        private static void PerformTeamBalancing()
        {
            if (IsWarmup() && !_config.TeamSwitch.BalanceDuringWarmup)
                return;

            if (!HasEnoughPlayers())
            {
                if (_config.General.EnableDebug)
                {
                    Console.WriteLine("[AdvancedTeamBalance] PerformTeamBalancing: Not enough players to balance.");
                }
                return;
            }

            PlayerManager.VerifyAllPlayers();
                
            // Get players by team
            var tPlayers = PlayerManager.GetPlayersByTeam(CsTeam.Terrorist);
            var ctPlayers = PlayerManager.GetPlayersByTeam(CsTeam.CounterTerrorist);
            
            if (_config.General.EnableDebug)
            {
                Console.WriteLine($"[AdvancedTeamBalance] PerformTeamBalancing: Initial check T:{tPlayers.Count} vs CT:{ctPlayers.Count}");
            }

            if (_config.Balancing.BalanceMode.StartsWith("Scramble", StringComparison.OrdinalIgnoreCase))
            {
                PerformTeamScramble();
                return;
            }
            
            var result = BalanceManager.BalanceTeams(tPlayers, ctPlayers, _tWinStreak, _ctWinStreak, _config.General.EnableDebug);
            
            if (_config.Messages.AnnounceBalancing)
            {
                if (result.PlayersMoved > 0)
                {
                    ChatHelper.PrintLocalizedChatAll(true, "balance.size.moved", result.PlayersMoved, 
                        result.PlayersMoved > 1 ? "s" : "");
                }
                
                // Only show skill swap messages if not OnlyBalanceByTeamSize
                if (result.SwapsMade > 0 && !_config.Balancing.OnlyBalanceByTeamSize)
                {
                    ChatHelper.PrintLocalizedChatAll(true, "balance.skill.swapped", result.SwapsMade,
                        result.SwapsMade > 1 ? "s" : "");
                }
            }
            
            ApplyTeamChanges(PlayerManager.GetPlayersByTeam(CsTeam.Terrorist), PlayerManager.GetPlayersByTeam(CsTeam.CounterTerrorist));

            PlayerManager.SyncPlayerData();
            
            var currentTPlayers = PlayerManager.GetPlayersByTeam(CsTeam.Terrorist);
            var currentCTPlayers = PlayerManager.GetPlayersByTeam(CsTeam.CounterTerrorist);
            int currentSizeDifference = Math.Abs(currentTPlayers.Count - currentCTPlayers.Count);

            if (_config.General.EnableDebug)
            {
                Console.WriteLine($"[AdvancedTeamBalance] Post-initial balance: T:{currentTPlayers.Count} vs CT:{currentCTPlayers.Count}. Difference: {currentSizeDifference}. MaxAllowed: {_config.TeamSwitch.MaxTeamSizeDifference}");
            }

            if (currentSizeDifference > _config.TeamSwitch.MaxTeamSizeDifference)
            {
                if (_config.General.EnableDebug)
                {
                    Console.WriteLine($"[AdvancedTeamBalance] Team sizes ({currentTPlayers.Count} vs {currentCTPlayers.Count}) still violate MaxTeamSizeDifference ({_config.TeamSwitch.MaxTeamSizeDifference}). Attempting ForceTeamSizeBalance.");
                }

                var forcedPlayersMoved = BalanceManager.ForceTeamSizeBalance(currentTPlayers, currentCTPlayers, _config.General.EnableDebug);
                
                if (forcedPlayersMoved > 0)
                {
                    if (_config.Messages.AnnounceBalancing)
                    {
                        ChatHelper.PrintLocalizedChatAll(true, "balance.force", forcedPlayersMoved, forcedPlayersMoved > 1 ? "s" : "");
                    }
                    
                    ApplyTeamChanges(PlayerManager.GetPlayersByTeam(CsTeam.Terrorist), PlayerManager.GetPlayersByTeam(CsTeam.CounterTerrorist));
                     if (_config.General.EnableDebug)
                    {
                         var finalT = PlayerManager.GetPlayersByTeam(CsTeam.Terrorist);
                         var finalCT = PlayerManager.GetPlayersByTeam(CsTeam.CounterTerrorist);
                         Console.WriteLine($"[AdvancedTeamBalance] After ForceTeamSizeBalance and ApplyTeamChanges: T:{finalT.Count} vs CT:{finalCT.Count}");
                    }
                }
                else
                {
                     if (_config.General.EnableDebug)
                    {
                        Console.WriteLine($"[AdvancedTeamBalance] ForceTeamSizeBalance moved 0 players, despite imbalance. Possible reasons: no eligible players on larger team (all alive/exempt).");
                    }
                }
            }
        }
        
        /// <summary>
        /// Perform a team scramble
        /// </summary>
        private static void PerformTeamScramble()
        {
            var allPlayers = PlayerManager.GetAllPlayers();
            bool success;

            if (_config.Balancing.BalanceMode.Equals("ScrambleRandom", StringComparison.OrdinalIgnoreCase))
            {
                success = BalanceManager.ScrambleTeamsRandom(allPlayers, _config.General.EnableDebug);
            }
            else
            {
                success = BalanceManager.ScrambleTeamsBySkill(allPlayers, _config.General.EnableDebug);
            }
            
            if (success && _config.Messages.AnnounceBalancing)
            {
                if (_config.Balancing.BalanceMode.Equals("ScrambleRandom", StringComparison.OrdinalIgnoreCase))
                {
                    ChatHelper.PrintLocalizedChatAll(true, "scramble.random");
                }
                else
                {
                    ChatHelper.PrintLocalizedChatAll(true, "scramble.skill");
                }
            }
            
            ApplyTeamChanges(
                PlayerManager.GetPlayersByTeam(CsTeam.Terrorist),
                PlayerManager.GetPlayersByTeam(CsTeam.CounterTerrorist)
            );
        }
        
        public static void ApplyTeamChanges(List<Player> tPlayers, List<Player> ctPlayers)
        {
            var controllers = Utilities.GetPlayers();
            
            foreach (var player in tPlayers)
            {
                var controller = controllers.FirstOrDefault(p => p.IsValid && !p.IsBot && p.SteamID == player.SteamId);
                if (controller != null && controller.TeamNum != (int)CsTeam.Terrorist)
                {
                    if (controller.PawnIsAlive)
                    {
                        if (_config.General.EnableDebug)
                        {
                            Console.WriteLine($"[AdvancedTeamBalance] Skipping alive player {player.Name} for team switch");
                        }
                        continue;
                    }
                    
                    controller.SwitchTeam(CsTeam.Terrorist);
                    
                    if (_config.Messages.NotifySwitchedPlayers)
                    {
                        ChatHelper.PrintLocalizedChat(controller, true, "player.moved.t");
                    }
                }
            }
            
            foreach (var player in ctPlayers)
            {
                var controller = controllers.FirstOrDefault(p => p.IsValid && !p.IsBot && p.SteamID == player.SteamId);
                if (controller != null && controller.TeamNum != (int)CsTeam.CounterTerrorist)
                {
                    if (controller.PawnIsAlive)
                    {
                        if (_config.General.EnableDebug)
                        {
                            Console.WriteLine($"[AdvancedTeamBalance] Skipping alive player {player.Name} for team switch");
                        }
                        continue;
                    }
                    
                    controller.SwitchTeam(CsTeam.CounterTerrorist);
                    
                    if (_config.Messages.NotifySwitchedPlayers)
                    {
                        ChatHelper.PrintLocalizedChat(controller, true, "player.moved.ct");
                    }
                }
            }
        }

        /// <summary>
        /// Resets all map-related statistics and counters when map changes
        /// </summary>
        public static void ResetMapStats()
        {
            _tWinStreak = 0;
            _ctWinStreak = 0;
            
            _gameRulesEntity = null;
            
            if (_config.General.EnableDebug)
            {
                Console.WriteLine("[AdvancedTeamBalance] EventManager stats reset: Win streaks and round counters cleared");
            }
        }

        private static void CheckGameRules()
        {
            if (_gameRulesEntity?.IsValid is not true)
            {
                _gameRulesEntity = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
            }
        }

        public static bool IsWarmup()
        {
            CheckGameRules();

            return _gameRulesEntity?.GameRules?.WarmupPeriod ?? false;
        }
    }
}
