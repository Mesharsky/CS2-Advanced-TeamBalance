using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    public Timer? FindAndSetSpawnsTimer { get; set; } = null;

    private List<CBaseEntity> ctSpawns = [];
    private List<CBaseEntity> ttSpawns = [];

    public void Initialize_Misc()
    {
        RegisterListener<Listeners.OnMapStart>((mapName) =>
        {
            FindAndSetSpawnsTimer?.Kill();
            FindAndSetSpawnsTimer = AddTimer(0.1f, () =>
            {
                FindAndSetSpawns();
                FindAndSetSpawnsTimer?.Kill();
            });
        });
    }

    public void FindAndSetSpawns()
    {
        ctSpawns = [];
        ttSpawns = [];

        ctSpawns = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("info_player_counterterrorist").ToList();         
        ttSpawns = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("info_player_terrorist").ToList();
    }

    private void CorrectPlayerSpawns()
    {
        var allPlayers = Utilities.GetPlayers();

        if (allPlayers == null || allPlayers.Count == 0)
        {
            PrintDebugMessage("No players found for spawn correction.");
            return;
        }

        foreach (var player in allPlayers)
        {
            if (player == null || !player.IsValid || player.IsBot || player.IsHLTV || player.Connected != PlayerConnectedState.PlayerConnected)
                continue;

            if (!player.PawnIsAlive)
                continue;

            if (IsInWrongSpawn(player))
            {
                TeleportPlayerToSpawn(player);
            }
        }
    }

    private bool IsInWrongSpawn(CCSPlayerController player)
    {
        var playerPosition = player.AbsOrigin;
        if (playerPosition == null)
            return false;

        List<CBaseEntity> enemySpawns = player.Team == CsTeam.CounterTerrorist ? ttSpawns : ctSpawns;

        foreach (var spawn in enemySpawns)
        {
            if (spawn == null || !spawn.IsValid || spawn.AbsOrigin == null)
                continue;

            const float positionThreshold = 1.0f;

            float distance = (spawn.AbsOrigin - playerPosition).Length();
            if (distance < positionThreshold)
            {
                return true;
            }
        }

        return false;
    }


    private void TeleportPlayerToSpawn(CCSPlayerController player)
    {
        List<CBaseEntity> teamSpawns = player.Team == CsTeam.CounterTerrorist ? ctSpawns : ttSpawns;

        if (teamSpawns == null || teamSpawns.Count == 0)
        {
            PrintDebugMessage("No spawn points available for team.");
            return;
        }

        var occupiedPositions = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && p.PawnIsAlive)
            .Select(p => p.AbsOrigin)
            .Where(pos => pos != null)
            .ToList();

        const float occupiedThreshold = 1.0f;

        foreach (var spawn in teamSpawns)
        {
            if (spawn == null || !spawn.IsValid || spawn.AbsOrigin == null)
                continue;

            bool isOccupied = occupiedPositions.Any(pos => (spawn.AbsOrigin - pos!).Length() < occupiedThreshold);
            if (!isOccupied)
            {
                var position = spawn.AbsOrigin;
                var angle = spawn.AbsRotation ?? new QAngle(0, 0, 0);
                var velocity = new Vector(0, 0, 0);

                player.PlayerPawn.Value!.Teleport(position, angle, velocity);

                PrintDebugMessage($"Teleported {player.PlayerName} to team's spawn point.");
                break;
            }
        }
    }

    private static void PrintDebugMessage(string message)
    {
        if (Config?.PluginSettings.EnableDebugMessages == true)
        {
            Console.WriteLine($"[Team Balance] {message}");
        }
    }

    private void PrintToChatAllMsg(string keyValue)
    {
        if (Config?.PluginSettings.EnableChatMessages == true)
        {
            Server.PrintToChatAll(ReplaceColorPlaceholders(string.Format(Localizer[keyValue], Config.PluginSettings.PluginTag)));
        }
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
                var newPlayer = new PlayerStats
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

    public static bool IsWarmup()
    {
        return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!
            .WarmupPeriod;
    }

    public static bool IsHalftime()
    {
        var gamerulesProxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
        var gamerules = gamerulesProxy?.GameRules;
        var halftimeEnabled = ConVar.Find("mp_halftime")?.GetPrimitiveValue<bool>() ?? false;
        var maxRounds = ConVar.Find("mp_maxrounds")?.GetPrimitiveValue<int>() ?? 0;

        if (gamerules == null || maxRounds == 0)
            return false;

        if (gamerules.GameRestart)
            return true;

        if (!halftimeEnabled)
            return false;

        int totalRoundsPlayed = gamerules.TotalRoundsPlayed;
        int roundsPerHalf = maxRounds / 2;

        return totalRoundsPlayed == roundsPerHalf;
    }


    public static bool IsOvertime()
    {
        var gamerulesProxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
        var gamerules = gamerulesProxy?.GameRules;
        var maxRounds = ConVar.Find("mp_maxrounds")?.GetPrimitiveValue<int>() ?? 0;
        var mp_overtime_enable = ConVar.Find("mp_overtime_enable")?.GetPrimitiveValue<bool>() ?? false;

        if (gamerules == null || maxRounds == 0)
            return false;

        if (gamerules.GameRestart)
            return true;

        if (!mp_overtime_enable)
            return false;

        int totalRoundsPlayed = gamerules.TotalRoundsPlayed;

        return totalRoundsPlayed >= maxRounds;
    }


    public static bool IsNextRoundHalftime()
    {
        var gamerulesProxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
        var gamerules = gamerulesProxy?.GameRules;
        var halftimeEnabled = ConVar.Find("mp_halftime")?.GetPrimitiveValue<bool>() ?? false;
        var maxRounds = ConVar.Find("mp_maxrounds")?.GetPrimitiveValue<int>() ?? 0;

        if (gamerules == null || !halftimeEnabled || maxRounds == 0)
            return false;

        int totalRoundsPlayed = gamerules.TotalRoundsPlayed;
        int roundsPerHalf = maxRounds / 2;

        return totalRoundsPlayed + 1 == roundsPerHalf;
    }

    public static bool IsNextRoundOvertime()
    {
        var gamerulesProxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
        var gamerules = gamerulesProxy?.GameRules;
        var maxRounds = ConVar.Find("mp_maxrounds")?.GetPrimitiveValue<int>() ?? 0;
        var mp_overtime_enable = ConVar.Find("mp_overtime_enable")?.GetPrimitiveValue<bool>() ?? false;

        if (gamerules == null || !mp_overtime_enable || maxRounds == 0)
            return false;

        int totalRoundsPlayed = gamerules.TotalRoundsPlayed;

        return totalRoundsPlayed + 1 > maxRounds;
    }

    private static readonly Dictionary<string, char> ColorMap = new()
    {
        { "[default]", ChatColors.Default },
        { "[white]", ChatColors.White },
        { "[darkred]", ChatColors.DarkRed },
        { "[green]", ChatColors.Green },
        { "[lightyellow]", ChatColors.LightYellow },
        { "[lightblue]", ChatColors.LightBlue },
        { "[olive]", ChatColors.Olive },
        { "[lime]", ChatColors.Lime },
        { "[red]", ChatColors.Red },
        { "[lightpurple]", ChatColors.LightPurple },
        { "[purple]", ChatColors.Purple },
        { "[grey]", ChatColors.Grey },
        { "[yellow]", ChatColors.Yellow },
        { "[gold]", ChatColors.Gold },
        { "[silver]", ChatColors.Silver },
        { "[blue]", ChatColors.Blue },
        { "[darkblue]", ChatColors.DarkBlue },
        { "[bluegrey]", ChatColors.BlueGrey },
        { "[magenta]", ChatColors.Magenta },
        { "[lightred]", ChatColors.LightRed },
        { "[orange]", ChatColors.Orange }
    };

    private static string ReplaceColorPlaceholders(string message)
    {
        foreach (var colorPlaceholder in ColorMap)
        {
            message = message.Replace(colorPlaceholder.Key, colorPlaceholder.Value.ToString());
        }
        return message;
    }
}
