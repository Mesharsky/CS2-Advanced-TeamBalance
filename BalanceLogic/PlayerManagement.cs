using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    private static readonly ConcurrentDictionary<ulong, PlayerStats> playerCache = new();

    public static void UpdatePlayerTeamsInCache()
    {
        PrintDebugMessage("Updating player teams in cache...");

        var allPlayers = Utilities.GetPlayers();

        foreach (var player in allPlayers)
        {
            if (player.IsBot)
            {
                continue;
            }

            if (playerCache.TryGetValue(player.SteamID, out var cachedPlayer))
            {
                cachedPlayer.Team = (int)player.Team;
            }
            else
            {
                var newPlayer = new PlayerStats
                {
                    PlayerName = player.PlayerName,
                    PlayerSteamID = player.SteamID,
                    Team = (int)player.Team,
                    Kills = player.ActionTrackingServices!.MatchStats.Kills,
                    Assists = player.ActionTrackingServices!.MatchStats.Assists,
                    Deaths = player.ActionTrackingServices.MatchStats.Deaths,
                    Damage = player.ActionTrackingServices.MatchStats.Damage,
                    Score = player.Score
                };

                playerCache.TryAdd(player.SteamID, newPlayer);
            }
        }
    }

    public static List<PlayerStats> GetPlayersForRebalance()
    {
        var players = playerCache.Values
            .Where(p => p.Team == (int)CsTeam.CounterTerrorist || p.Team == (int)CsTeam.Terrorist)
            .OrderByDescending(p => p.PerformanceScore)
            .ToList();

        PrintDebugMessage($"Total valid players for rebalance: {players.Count}");
        return players;
    }
}
