<<<<<<< HEAD
<<<<<<< HEAD
=======
>>>>>>> 182a34f (Merge local)
using CounterStrikeSharp.API.Modules.Utils;

namespace AdvancedTeamBalance
{
    /// <summary>
    /// Represents a player with their statistics and team information
    /// </summary>
    // Add this property to the Player class
    public class Player(ulong steamId, string name, CsTeam team)
    {
        public ulong SteamId { get; set; } = steamId;
        public string Name { get; set; } = name;
        public CsTeam Team { get; set; } = team;
        public bool IsConnected { get; set; } = true;
        public bool IsExemptFromSwitching { get; set; }
        
        public bool IsAlive { get; set; } = false;
        
        public PlayerStats Stats { get; private set; } = new PlayerStats();

        public DateTime LastTeamSwitchTime { get; set; } = DateTime.UtcNow;
        public int RoundsOnCurrentTeam { get; set; }
        public int ImmunityTimeRemaining { get; set; }

        public bool CanBeSwitched(int minRoundsBeforeSwitch)
        {
            if (IsExemptFromSwitching || ImmunityTimeRemaining > 0)
                return false;
                
            if (RoundsOnCurrentTeam < minRoundsBeforeSwitch)
                return false;
                
            // Don't switch alive players
            if (IsAlive)
                return false;
                
            return true;
        }
        
        /// <summary>
        /// Updates the player's team state
        /// </summary>
        public void UpdateTeamState(CsTeam newTeam, int immunityTime = 0)
        {
            Team = newTeam;
            RoundsOnCurrentTeam = 0;
            LastTeamSwitchTime = DateTime.UtcNow;
            ImmunityTimeRemaining = immunityTime;
        }
    }
    
    /// <summary>
    /// Contains player performance statistics
    /// </summary>
    public class PlayerStats
    {
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public int Score { get; set; }
        public int RoundsPlayed { get; set; }
        public int RoundsWon { get; set; }
        
        public double KDRatio => Deaths == 0 ? Kills : (double)Kills / Deaths;
        public double KDARatio => Deaths == 0 ? (Kills + (Assists * 0.5)) : (Kills + (Assists * 0.5)) / Deaths;
        public double WinRate => RoundsPlayed == 0 ? 0 : (double)RoundsWon / RoundsPlayed * 100;
        
        public void Reset()
        {
            Kills = 0;
            Deaths = 0;
            Assists = 0;
            Score = 0;
            RoundsPlayed = 0;
            RoundsWon = 0;
        }
    }
<<<<<<< HEAD
=======
using CounterStrikeSharp.API.Modules.Utils;

namespace AdvancedTeamBalance
{
    /// <summary>
    /// Represents a player with their statistics and team information
    /// </summary>
    // Add this property to the Player class
    public class Player(ulong steamId, string name, CsTeam team)
    {
        public ulong SteamId { get; set; } = steamId;
        public string Name { get; set; } = name;
        public CsTeam Team { get; set; } = team;
        public bool IsConnected { get; set; } = true;
        public bool IsExemptFromSwitching { get; set; }
        
        public bool IsAlive { get; set; } = false;
        
        public PlayerStats Stats { get; private set; } = new PlayerStats();

        public DateTime LastTeamSwitchTime { get; set; } = DateTime.UtcNow;
        public int RoundsOnCurrentTeam { get; set; }
        public int ImmunityTimeRemaining { get; set; }

        public bool CanBeSwitched(int minRoundsBeforeSwitch)
        {
            if (IsExemptFromSwitching || ImmunityTimeRemaining > 0)
                return false;
                
            if (RoundsOnCurrentTeam < minRoundsBeforeSwitch)
                return false;
                
            // Don't switch alive players
            if (IsAlive)
                return false;
                
            return true;
        }
        
        /// <summary>
        /// Updates the player's team state
        /// </summary>
        public void UpdateTeamState(CsTeam newTeam, int immunityTime = 0)
        {
            Team = newTeam;
            RoundsOnCurrentTeam = 0;
            LastTeamSwitchTime = DateTime.UtcNow;
            ImmunityTimeRemaining = immunityTime;
        }
    }
    
    /// <summary>
    /// Contains player performance statistics
    /// </summary>
    public class PlayerStats
    {
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public int Score { get; set; }
        public int RoundsPlayed { get; set; }
        public int RoundsWon { get; set; }
        
        public double KDRatio => Deaths == 0 ? Kills : (double)Kills / Deaths;
        public double KDARatio => Deaths == 0 ? (Kills + (Assists * 0.5)) : (Kills + (Assists * 0.5)) / Deaths;
        public double WinRate => RoundsPlayed == 0 ? 0 : (double)RoundsWon / RoundsPlayed * 100;
        
        public void Reset()
        {
            Kills = 0;
            Deaths = 0;
            Assists = 0;
            Score = 0;
            RoundsPlayed = 0;
            RoundsWon = 0;
        }
    }
>>>>>>> 74fc461956bce7b38451c220f391a210b6af4d41
=======
>>>>>>> 182a34f (Merge local)
}