using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using static Mesharsky_TeamBalance.GameRules;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    public void Initialize_Events()
    {
        Event_PlayerDisconnect();
        Event_RoundStartPre();
    }

    public void Event_RoundStartPre()
    {
        RegisterEventHandler((EventRoundPrestart @event, GameEventInfo info) =>
        {
            UpdatePlayerStatsInCache();
            AttemptBalanceTeams();

            return HookResult.Continue;
        });
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (IsWarmup())
            return HookResult.Continue;

        var player = @event.Userid;

        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected)
            return HookResult.Continue;

        if (!player.PawnIsAlive)
            return HookResult.Continue;

        ValidatePlayerModel(player);
        
        AddTimer(0.5f, () =>
        {
            if (IsInWrongSpawn(player))
            {
                TeleportPlayerToSpawn(player);
                ValidatePlayerModel(player);
            }
        });

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (GlobalBalanceMade)
        {
            if (balanceStats.WasLastActionScramble)
            {
                PrintToChatAllMsg("teams.scrambled");
            }
            else
            {
                PrintToChatAllMsg("teams.balanced");
            }
        }
        else
        {
            PrintToChatAllMsg("teams.balance.not.needed");
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        UpdatePlayerStatsInCache();

        bool ctWin = @event.Winner == (int)CsTeam.CounterTerrorist;
        balanceStats.UpdateStreaks(ctWin);

        return HookResult.Continue;
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

    private static void UpdatePlayerStatsInCache()
    {
        PrintDebugMessage("Updating player stats in cache...");

        var allPlayers = Utilities.GetPlayers();

        if (allPlayers.Count == 0)
        {
            PrintDebugMessage("No players found.");
            return;
        }

        foreach (var player in allPlayers)
        {
            if (player.IsBot)
            {
                continue;
            }

            if (playerCache.TryGetValue(player.SteamID, out var cachedPlayer))
            {
                cachedPlayer.Kills = player.ActionTrackingServices!.MatchStats.Kills;
                cachedPlayer.Assists = player.ActionTrackingServices!.MatchStats.Assists;
                cachedPlayer.Deaths = player.ActionTrackingServices.MatchStats.Deaths;
                cachedPlayer.Damage = player.ActionTrackingServices.MatchStats.Damage;
                cachedPlayer.Score = player.Score;
            }
            else
            {
                var newPlayer = new PlayerStats
                {
                    PlayerName = player.PlayerName,
                    PlayerSteamID = player.SteamID,
                    Team = player.TeamNum,
                    Kills = player.ActionTrackingServices!.MatchStats.Kills,
                    Assists = player.ActionTrackingServices!.MatchStats.Assists,
                    Deaths = player.ActionTrackingServices.MatchStats.Deaths,
                    Damage = player.ActionTrackingServices.MatchStats.Damage,
                    Score = player.Score,
                };

                playerCache.TryAdd(player.SteamID, newPlayer);
                PrintDebugMessage($"Added {newPlayer.PlayerName} to cache with stats.");
            }
        }
    }

    private HookResult Command_JoinTeam(CCSPlayerController? player, CommandInfo info)
    {
        if (IsWarmup())
            return HookResult.Continue;

        if (player == null)
            return HookResult.Continue;

        int teamId = ParseTeamId(info);

        if (teamId == (int)CsTeam.Spectator)
        {
            if (playerCache.TryGetValue(player.SteamID, out var cPlayer))
            {
                UpdateTeamAssignment(cPlayer, teamId);
            }
            return HookResult.Continue;
        }

        if (teamId < (int)CsTeam.Spectator || teamId > (int)CsTeam.CounterTerrorist)
            return HookResult.Continue;

        if (!playerCache.TryGetValue(player.SteamID, out var cachedPlayer))
            return HookResult.Continue;

        if (cachedPlayer.Team == teamId)
        {
            PrintDebugMessage($"Player {cachedPlayer.PlayerName} is already on team {teamId}. No change needed.");
            return HookResult.Continue;
        }

        if (!CanSwitchTeams(cachedPlayer, teamId))
        {
            PrintDebugMessage($"Player {cachedPlayer.PlayerName} cannot switch to team {teamId} as it would violate the team balance.");

            player.PrintToChat(StringExtensions.ReplaceColorTags(string.Format(Localizer["teams.join.block"], Config?.PluginSettings.PluginTag)));
            return HookResult.Handled;
        }

        UpdateTeamAssignment(cachedPlayer, teamId);
        return HookResult.Continue;
    }

    private static int ParseTeamId(CommandInfo info)
    {
        int startIndex = info.ArgByIndex(0).ToLower() == "jointeam" ? 1 : 0;
        return info.ArgCount > startIndex && int.TryParse(info.ArgByIndex(startIndex), out int teamId) ? teamId : -1;
    }

    private static bool CanSwitchTeams(PlayerStats cachedPlayer, int newTeamId)
    {
        int ctPlayerCount = playerCache.Values.Count(p => p.Team == (int)CsTeam.CounterTerrorist);
        int tPlayerCount = playerCache.Values.Count(p => p.Team == (int)CsTeam.Terrorist);

        AdjustPlayerCountForSwitch(cachedPlayer, newTeamId, ref ctPlayerCount, ref tPlayerCount);

        return Math.Abs(ctPlayerCount - tPlayerCount) <= Config?.PluginSettings.MaxTeamSizeDifference;
    }

    private static void AdjustPlayerCountForSwitch(PlayerStats cachedPlayer, int newTeamId, ref int ctPlayerCount, ref int tPlayerCount)
    {
        if (cachedPlayer.Team == (int)CsTeam.CounterTerrorist)
            ctPlayerCount--;
        else if (cachedPlayer.Team == (int)CsTeam.Terrorist)
            tPlayerCount--;

        if (newTeamId == (int)CsTeam.CounterTerrorist)
            ctPlayerCount++;
        else if (newTeamId == (int)CsTeam.Terrorist)
            tPlayerCount++;
    }

    private static void UpdateTeamAssignment(PlayerStats cachedPlayer, int newTeamId)
    {
        cachedPlayer.Team = newTeamId;
        PrintDebugMessage($"Player {cachedPlayer.PlayerName} updated to team {newTeamId} in cache.");
    }
}
