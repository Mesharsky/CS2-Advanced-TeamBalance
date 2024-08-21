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
    public HookResult OnRoundPreStart(EventRoundPrestart @event, GameEventInfo info)
    {
        AttemptBalanceTeams();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (BalanceHasBeenMade)
        {
            Server.PrintToChatAll($"{ChatColors.Red}[Balans Drużyn] {ChatColors.Default}Drużyny zostały zbalansowane");
            PrintDebugMessage("Teams have been balanced.");
        }
        return HookResult.Continue;
    }

    public void Event_PlayerJoin()
    {
        RegisterListener<Listeners.OnClientPutInServer>((slot) =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player != null && player.IsValid && !player.IsBot)
            {
                ProcessUserInformation(player);
            }
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
            Team = (int)player.Team,
            Score = player.Score
        };

        playerCache.AddOrUpdate(steamId, cachedPlayer, (key, oldPlayer) => cachedPlayer);
        PrintDebugMessage($"Player {player.PlayerName} with SteamID {steamId} added to cache.");
    }
}
