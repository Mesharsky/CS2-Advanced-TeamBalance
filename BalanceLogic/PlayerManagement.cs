using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    private static readonly ConcurrentDictionary<ulong, Player> playerCache = new();

    public static void UpdatePlayerTeamsInCache()
    {
        PrintDebugMessage("Updating player teams in cache...");

        var allPlayers = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected);

        foreach (var player in allPlayers)
        {
            if (playerCache.TryGetValue(player.SteamID, out var cachedPlayer))
            {
                cachedPlayer.Team = (int)player.Team;
            }
            else
            {
                var newPlayer = new Player
                {
                    PlayerName = player.PlayerName,
                    PlayerSteamID = player.SteamID,
                    Team = (int)player.Team,
                    Score = player.Score
                };

                playerCache.TryAdd(player.SteamID, newPlayer);
            }
        }
    }

    public static List<Player> GetPlayersForRebalance()
    {
        var players = playerCache.Values
            .Where(p => p.Team == (int)CsTeam.CounterTerrorist || p.Team == (int)CsTeam.Terrorist)
            .OrderByDescending(p => Config?.PluginSettings.UsePerformanceScore == true ? p.PerformanceScore : p.Score)
            .ToList();

        PrintDebugMessage($"Total valid players for rebalance: {players.Count}");
        return players;
    }
}
