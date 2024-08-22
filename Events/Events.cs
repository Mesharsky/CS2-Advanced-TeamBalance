using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    private static readonly ConcurrentDictionary<ulong, Player> playerCache = new();

    public void Initialize_Events()
    {
        Event_PlayerJoin();
        Event_PlayerDisconnect();
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        UpdatePlayerStatsInCache();
        AttemptBalanceTeams();

        return HookResult.Continue;
    }

    public void Event_PlayerJoin()
    {
        RegisterListener<Listeners.OnClientPutInServer>((slot) =>
        {
            AddTimer(3.0f, () =>
            {
                var player = Utilities.GetPlayerFromSlot(slot);
                if (player == null || !player.IsValid || player.IsBot)
                    return;

                ProcessUserInformation(player);
            });
        });
    }

    public void Event_PlayerDisconnect()
    {
        RegisterEventHandler((EventPlayerDisconnect @event, GameEventInfo info) =>
        {
            var player = @event.Userid;
            if (player == null)
                return HookResult.Continue;

            if (playerCache.TryRemove(player.SteamID, out _))
            {
                PrintDebugMessage($"Player {player.PlayerName} removed from cache.");
            }

            return HookResult.Continue;
        });
    }

    private static void ProcessUserInformation(CCSPlayerController player)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return;

        ulong steamId = player.SteamID;
        var cachedPlayer = new Player
        {
            PlayerName = player.PlayerName,
            PlayerSteamID = steamId,
            Team = player.TeamNum,
            Kills = 0,
            Deaths = 0,
            Damage = 0,
            Score = 0,
        };

        playerCache.AddOrUpdate(steamId, cachedPlayer, (key, oldPlayer) => cachedPlayer);
        PrintDebugMessage($"Player {player.PlayerName} with SteamID {steamId} added to cache.");
    }

    private static void UpdatePlayerStatsInCache()
    {
        PrintDebugMessage("Updating player stats in cache...");

        var allPlayers = Utilities.GetPlayers();

        foreach (var player in allPlayers.Where(p => p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected))
        {
            if (playerCache.TryGetValue(player.SteamID, out var cachedPlayer))
            {
                cachedPlayer.Kills = player.ActionTrackingServices!.MatchStats.Kills;
                cachedPlayer.Deaths = player.ActionTrackingServices.MatchStats.Deaths;
                cachedPlayer.Damage = player.ActionTrackingServices.MatchStats.Damage;
                cachedPlayer.Score = player.Score;
                PrintDebugMessage($"Updated {cachedPlayer.PlayerName} stats in cache.");
            }
            else
            {
                var newPlayer = new Player
                {
                    PlayerName = player.PlayerName,
                    PlayerSteamID = player.SteamID,
                    Team = player.TeamNum,
                    Kills = player.ActionTrackingServices!.MatchStats.Kills,
                    Deaths = player.ActionTrackingServices.MatchStats.Deaths,
                    Damage = player.ActionTrackingServices.MatchStats.Damage,
                    Score = player.Score,
                };

                playerCache.TryAdd(player.SteamID, newPlayer);
                PrintDebugMessage($"Added {newPlayer.PlayerName} to cache with stats.");
            }
        }
    }
}
