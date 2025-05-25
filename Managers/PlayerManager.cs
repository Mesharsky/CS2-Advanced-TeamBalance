<<<<<<< HEAD
using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;

namespace AdvancedTeamBalance
{
    /// <summary>
    /// Manages player data and provides access to player statistics
    /// </summary>
    public static class PlayerManager
    {
        private static readonly ConcurrentDictionary<ulong, Player> _players = new();
        private static PluginConfig _config = null!;

        public static void Initialize(PluginConfig config)
        {
            _config = config;
        }
        
        /// <summary>
        /// Get or add a player to the tracking system
        /// </summary>
        public static Player GetOrAddPlayer(CCSPlayerController controller)
        {
            if (_players.TryGetValue(controller.SteamID, out var player))
            {
                if (player.Name != controller.PlayerName)
                {
                    player.Name = controller.PlayerName;
                }
                
                if (!player.IsConnected)
                {
                    player.IsConnected = true;
                }
                
                return player;
            }
            
            var newPlayer = new Player(controller.SteamID, controller.PlayerName, (CsTeam)controller.TeamNum);
            _players[controller.SteamID] = newPlayer;
            
            if (_config.Admin.ExcludeAdmins)
            {
                newPlayer.IsExemptFromSwitching = IsPlayerAdmin(controller);
            }
            
            return newPlayer;
        }
        
        private static bool IsPlayerAdmin(CCSPlayerController player)
        {
            if (player == null || !player.IsValid)
                return false;
                
            return CounterStrikeSharp.API.Modules.Admin.AdminManager.PlayerHasPermissions(
                player, 
                _config.Admin.AdminExemptFlag
            );
        }
        
        /// <summary>
        /// Get all active players
        /// </summary>
        public static List<Player> GetAllPlayers()
        {
            return [.. _players.Values.Where(p => p.IsConnected)];
        }
        
        /// <summary>
        /// Get all players on a specific team
        /// </summary>
        public static List<Player> GetPlayersByTeam(CsTeam team)
        {
            return [.. _players.Values.Where(p => p.IsConnected && p.Team == team)];
        }
        
        public static void MarkPlayerDisconnected(ulong steamId)
        {
            if (_players.TryGetValue(steamId, out var player))
            {
                player.IsConnected = false;
            }
        }
        
        /// <summary>
        /// Get the current team balance
        /// </summary>
        public static (int TCount, int CTCount) GetTeamCounts()
        {
            var tCount = _players.Values.Count(p => p.IsConnected && p.Team == CsTeam.Terrorist);
            var ctCount = _players.Values.Count(p => p.IsConnected && p.Team == CsTeam.CounterTerrorist);
            return (tCount, ctCount);
        }
        
        /// <summary>
        /// Reset all tracking
        /// </summary>
        public static void Cleanup()
        {
            _players.Clear();
        }

        /// <summary>
        /// Verifies all tracked players against actual server state and cleans up stale entries
        /// </summary>
        public static void VerifyAllPlayers()
        {
            if (_config.General.EnableDebug)
            {
                Console.WriteLine("[AdvancedTeamBalance] Verifying all player data against server state...");
            }

            // Get all current server players
            var connectedPlayers = new HashSet<ulong>();
            var controllers = Utilities.GetPlayers();
            
            // First pass: update all active players and mark which ones are really connected
            foreach (var controller in controllers)
            {
                if (controller != null && controller.IsValid && !controller.IsBot)
                {
                    connectedPlayers.Add(controller.SteamID);
                    
                    if (_players.TryGetValue(controller.SteamID, out var player))
                    {
                        if (player.Name != controller.PlayerName)
                        {
                            player.Name = controller.PlayerName;
                        }
                        
                        player.IsConnected = true;
                        
                        CsTeam actualTeam = (CsTeam)controller.TeamNum;
                        if (player.Team != actualTeam)
                        {
                            if (_config.General.EnableDebug)
                            {
                                Console.WriteLine($"[AdvancedTeamBalance] Fixing team mismatch for {player.Name}: Tracked={player.Team}, Actual={actualTeam}");
                            }
                            player.Team = actualTeam;
                        }
                    }
                    else
                    {
                        _players[controller.SteamID] = new Player(controller.SteamID, controller.PlayerName, (CsTeam)controller.TeamNum);
                        if (_config.General.EnableDebug)
                        {
                            Console.WriteLine($"[AdvancedTeamBalance] Added missing player: {controller.PlayerName}");
                        }
                    }
                }
            }
            
            int disconnectedCount = 0;
            foreach (var player in _players.Values)
            {
                if (player.IsConnected && !connectedPlayers.Contains(player.SteamId))
                {
                    player.IsConnected = false;
                    disconnectedCount++;
                    
                    if (_config.General.EnableDebug)
                    {
                        Console.WriteLine($"[AdvancedTeamBalance] Marking stale player as disconnected: {player.Name}");
                    }
                }
            }
            
            if (_config.General.EnableDebug)
            {
                var (tCount, ctCount) = GetTeamCounts();
                Console.WriteLine($"[AdvancedTeamBalance] Verification complete. Fixed {disconnectedCount} stale entries.");
                Console.WriteLine($"[AdvancedTeamBalance] Current verified team counts - T: {tCount}, CT: {ctCount}");
                
                int actualTCount = controllers.Count(p => p.IsValid && !p.IsBot && p.TeamNum == (int)CsTeam.Terrorist);
                int actualCTCount = controllers.Count(p => p.IsValid && !p.IsBot && p.TeamNum == (int)CsTeam.CounterTerrorist);
                Console.WriteLine($"[AdvancedTeamBalance] Actual server team counts - T: {actualTCount}, CT: {actualCTCount}");
            }
        }

        public static void SyncPlayerData()
        {
            var controllers = Utilities.GetPlayers();
            foreach (var controller in controllers)
            {
                if (controller != null && controller.IsValid && !controller.IsBot)
                {
                    var player = GetOrAddPlayer(controller);
                    
                    if (player.Team != (CsTeam)controller.TeamNum)
                    {
                        if (_config.General.EnableDebug)
                        {
                            Console.WriteLine($"[AdvancedTeamBalance] Syncing player {player.Name} team: {player.Team} → {(CsTeam)controller.TeamNum}");
                        }
                        player.Team = (CsTeam)controller.TeamNum;
                    }
                }
            }
        }

        /// <summary>
        /// Resets all player statistics when the map changes
        /// </summary>
        public static void ResetAllPlayerStats()
        {
            foreach (var player in _players.Values)
            {
                if (player.IsConnected)
                {
                    // Reset player statistics
                    player.Stats.Reset();
                    
                    // Reset team-related counters
                    player.RoundsOnCurrentTeam = 0;
                    player.ImmunityTimeRemaining = 0;
                    
                    if (_config.General.EnableDebug)
                    {
                        Console.WriteLine($"[AdvancedTeamBalance] Reset stats for player: {player.Name}");
                    }
                }
            }
            
            if (_config.General.EnableDebug)
            {
                Console.WriteLine($"[AdvancedTeamBalance] PlayerManager: Reset stats for {_players.Values.Count(p => p.IsConnected)} connected players");
            }
        }
    }
=======
using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API;

namespace AdvancedTeamBalance
{
    /// <summary>
    /// Manages player data and provides access to player statistics
    /// </summary>
    public static class PlayerManager
    {
        private static readonly ConcurrentDictionary<ulong, Player> _players = new();
        private static PluginConfig _config = null!;

        public static void Initialize(PluginConfig config)
        {
            _config = config;
        }
        
        /// <summary>
        /// Get or add a player to the tracking system
        /// </summary>
        public static Player GetOrAddPlayer(CCSPlayerController controller)
        {
            if (_players.TryGetValue(controller.SteamID, out var player))
            {
                if (player.Name != controller.PlayerName)
                {
                    player.Name = controller.PlayerName;
                }
                
                if (!player.IsConnected)
                {
                    player.IsConnected = true;
                }
                
                return player;
            }
            
            var newPlayer = new Player(controller.SteamID, controller.PlayerName, (CsTeam)controller.TeamNum);
            _players[controller.SteamID] = newPlayer;
            
            if (_config.Admin.ExcludeAdmins)
            {
                newPlayer.IsExemptFromSwitching = IsPlayerAdmin(controller);
            }
            
            return newPlayer;
        }
        
        private static bool IsPlayerAdmin(CCSPlayerController player)
        {
            if (player == null || !player.IsValid)
                return false;
                
            return CounterStrikeSharp.API.Modules.Admin.AdminManager.PlayerHasPermissions(
                player, 
                _config.Admin.AdminExemptFlag
            );
        }
        
        /// <summary>
        /// Get all active players
        /// </summary>
        public static List<Player> GetAllPlayers()
        {
            return _players.Values.Where(p => p.IsConnected).ToList();
        }
        
        /// <summary>
        /// Get all players on a specific team
        /// </summary>
        public static List<Player> GetPlayersByTeam(CsTeam team)
        {
            return _players.Values.Where(p => p.IsConnected && p.Team == team).ToList();
        }
        
        public static void MarkPlayerDisconnected(ulong steamId)
        {
            if (_players.TryGetValue(steamId, out var player))
            {
                player.IsConnected = false;
            }
        }
        
        /// <summary>
        /// Get the current team balance
        /// </summary>
        public static (int TCount, int CTCount) GetTeamCounts()
        {
            var tCount = _players.Values.Count(p => p.IsConnected && p.Team == CsTeam.Terrorist);
            var ctCount = _players.Values.Count(p => p.IsConnected && p.Team == CsTeam.CounterTerrorist);
            return (tCount, ctCount);
        }
        
        /// <summary>
        /// Reset all tracking
        /// </summary>
        public static void Cleanup()
        {
            _players.Clear();
        }

        /// <summary>
        /// Verifies all tracked players against actual server state and cleans up stale entries
        /// </summary>
        public static void VerifyAllPlayers()
        {
            if (_config.General.EnableDebug)
            {
                Console.WriteLine("[AdvancedTeamBalance] Verifying all player data against server state...");
            }

            // Get all current server players
            var connectedPlayers = new HashSet<ulong>();
            var controllers = Utilities.GetPlayers();
            
            // First pass: update all active players and mark which ones are really connected
            foreach (var controller in controllers)
            {
                if (controller != null && controller.IsValid && !controller.IsBot)
                {
                    connectedPlayers.Add(controller.SteamID);
                    
                    if (_players.TryGetValue(controller.SteamID, out var player))
                    {
                        if (player.Name != controller.PlayerName)
                        {
                            player.Name = controller.PlayerName;
                        }
                        
                        player.IsConnected = true;
                        
                        CsTeam actualTeam = (CsTeam)controller.TeamNum;
                        if (player.Team != actualTeam)
                        {
                            if (_config.General.EnableDebug)
                            {
                                Console.WriteLine($"[AdvancedTeamBalance] Fixing team mismatch for {player.Name}: Tracked={player.Team}, Actual={actualTeam}");
                            }
                            player.Team = actualTeam;
                        }
                    }
                    else
                    {
                        _players[controller.SteamID] = new Player(controller.SteamID, controller.PlayerName, (CsTeam)controller.TeamNum);
                        if (_config.General.EnableDebug)
                        {
                            Console.WriteLine($"[AdvancedTeamBalance] Added missing player: {controller.PlayerName}");
                        }
                    }
                }
            }
            
            int disconnectedCount = 0;
            foreach (var player in _players.Values)
            {
                if (player.IsConnected && !connectedPlayers.Contains(player.SteamId))
                {
                    player.IsConnected = false;
                    disconnectedCount++;
                    
                    if (_config.General.EnableDebug)
                    {
                        Console.WriteLine($"[AdvancedTeamBalance] Marking stale player as disconnected: {player.Name}");
                    }
                }
            }
            
            if (_config.General.EnableDebug)
            {
                var (tCount, ctCount) = GetTeamCounts();
                Console.WriteLine($"[AdvancedTeamBalance] Verification complete. Fixed {disconnectedCount} stale entries.");
                Console.WriteLine($"[AdvancedTeamBalance] Current verified team counts - T: {tCount}, CT: {ctCount}");
                
                int actualTCount = controllers.Count(p => p.IsValid && !p.IsBot && p.TeamNum == (int)CsTeam.Terrorist);
                int actualCTCount = controllers.Count(p => p.IsValid && !p.IsBot && p.TeamNum == (int)CsTeam.CounterTerrorist);
                Console.WriteLine($"[AdvancedTeamBalance] Actual server team counts - T: {actualTCount}, CT: {actualCTCount}");
            }
        }

        public static void SyncPlayerData()
        {
            var controllers = Utilities.GetPlayers();
            foreach (var controller in controllers)
            {
                if (controller != null && controller.IsValid && !controller.IsBot)
                {
                    var player = GetOrAddPlayer(controller);
                    
                    if (player.Team != (CsTeam)controller.TeamNum)
                    {
                        if (_config.General.EnableDebug)
                        {
                            Console.WriteLine($"[AdvancedTeamBalance] Syncing player {player.Name} team: {player.Team} → {(CsTeam)controller.TeamNum}");
                        }
                        player.Team = (CsTeam)controller.TeamNum;
                    }
                }
            }
        }

        /// <summary>
        /// Resets all player statistics when the map changes
        /// </summary>
        public static void ResetAllPlayerStats()
        {
            foreach (var player in _players.Values)
            {
                if (player.IsConnected)
                {
                    // Reset player statistics
                    player.Stats.Reset();
                    
                    // Reset team-related counters
                    player.RoundsOnCurrentTeam = 0;
                    player.ImmunityTimeRemaining = 0;
                    
                    if (_config.General.EnableDebug)
                    {
                        Console.WriteLine($"[AdvancedTeamBalance] Reset stats for player: {player.Name}");
                    }
                }
            }
            
            if (_config.General.EnableDebug)
            {
                Console.WriteLine($"[AdvancedTeamBalance] PlayerManager: Reset stats for {_players.Values.Count(p => p.IsConnected)} connected players");
            }
        }
    }
>>>>>>> 74fc461956bce7b38451c220f391a210b6af4d41
}