using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    public void Initialize_Events()
    {
        Event_PlayerDisconnect();
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (GlobalBalanceMade)
        {
            Server.PrintToChatAll($" {ChatColors.Red}[Team Balance] {ChatColors.Default}Teams have been balanced.");
        }
        else
        {
            Server.PrintToChatAll($" {ChatColors.Red}[Team Balance] {ChatColors.Default}No need for team balance at this moment.");
        }
        if (!IsWarmup())
            return HookResult.Continue;

        var endTime = ConVar.Find("mp_warmuptime")?.GetPrimitiveValue<float>();
        var delay = endTime == null ? 1 : (endTime - 1);

        AddTimer((float)delay, AttemptBalanceTeams);

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        UpdatePlayerStatsInCache();
        var endTime = ConVar.Find("mp_round_restart_delay")?.GetPrimitiveValue<float>();
        var delay = endTime == null ? 1 : (endTime - 1);

        AddTimer((float)delay, AttemptBalanceTeams);

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

        foreach (var player in allPlayers.Where(p => p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected))
        {
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
                var newPlayer = new Player
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

        if (player == null || !player.IsValid)
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
            player.PrintToChat($" {ChatColors.Red}[Team Balance] {ChatColors.Default} you cannot switch to this team as it would violate the team balance");
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

    private static bool CanSwitchTeams(Player cachedPlayer, int newTeamId)
    {
        int ctPlayerCount = playerCache.Values.Count(p => p.Team == (int)CsTeam.CounterTerrorist);
        int tPlayerCount = playerCache.Values.Count(p => p.Team == (int)CsTeam.Terrorist);

        AdjustPlayerCountForSwitch(cachedPlayer, newTeamId, ref ctPlayerCount, ref tPlayerCount);

        return Math.Abs(ctPlayerCount - tPlayerCount) <= Config?.PluginSettings.MaxTeamSizeDifference;
    }

    private static void AdjustPlayerCountForSwitch(Player cachedPlayer, int newTeamId, ref int ctPlayerCount, ref int tPlayerCount)
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

    private static void UpdateTeamAssignment(Player cachedPlayer, int newTeamId)
    {
        cachedPlayer.Team = newTeamId;
        PrintDebugMessage($"Player {cachedPlayer.PlayerName} updated to team {newTeamId} in cache.");
    }
}