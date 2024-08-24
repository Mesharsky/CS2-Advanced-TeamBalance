using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    private static void PrintDebugMessage(string message)
    {
        Console.WriteLine($"[Team Balance] {message}");
    }

    private static bool ChangePlayerTeam(ulong steamId, CsTeam newTeam)
    {
        var player = Utilities.GetPlayerFromSteamId(steamId);
        if (player == null || !player.IsValid)
        {
            PrintDebugMessage($"Failed to switch team for player with SteamID: {steamId}. Player not found or invalid.");
            return false;
        }

        if (player.Team == newTeam)
        {
            PrintDebugMessage($"Player {player.PlayerName} is already in team {newTeam}. No switch needed.");
            return true;
        }

        player.SwitchTeam(newTeam);

        playerCache.AddOrUpdate(steamId, 
            (key) =>
            {
                var newPlayer = new Player
                {
                    PlayerName = player.PlayerName,
                    PlayerSteamID = player.SteamID,
                    Team = (int)newTeam,
                    Score = player.Score
                };
                PrintDebugMessage($"Added {newPlayer.PlayerName} to cache with team {newPlayer.Team}");
                return newPlayer;
            }, 
            (key, cachedPlayer) =>
            {
                cachedPlayer.Team = (int)newTeam;
                PrintDebugMessage($"Player {cachedPlayer.PlayerName} switched to {newTeam} and updated in cache.");
                return cachedPlayer;
            });

        return true;
    }

    private static bool CanMovePlayer(List<Player> fromTeam, List<Player> toTeam, Player player, int currentRound)
    {
        // Check if moving the player would exceed max team size difference
        if (Math.Abs(fromTeam.Count - 1 - (toTeam.Count + 1)) > Config?.PluginSettings.MaxTeamSizeDifference)
        {
            return false;
        }

        return true;
    }

    public static int GetCurrentRound()
    {
        var gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;

        int rounds = gameRules.TotalRoundsPlayed;
        
        return rounds;
    }

    public static bool IsWarmup()
    {
        return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!
            .WarmupPeriod;
    }
}
