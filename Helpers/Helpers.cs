using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    private List<CBaseEntity> ctSpawns = [];
    private List<CBaseEntity> ttSpawns = [];

    public void Initialize_Misc()
    {
        RegisterListener<Listeners.OnMapStart>((mapName) =>
        {
            AddTimer(1.0f, () =>
            {
                FindAndSetSpawns();
            });
        });
    }

    public void FindAndSetSpawns()
    {
        ctSpawns = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("info_player_counterterrorist").ToList();         
        ttSpawns = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("info_player_terrorist").ToList();

        PrintDebugMessage($"[TeamBalance] CT Spawns: {ctSpawns.Count} -- Terrorist Spawns: {ttSpawns.Count}");
    }

    private bool IsInWrongSpawn(CCSPlayerController player)
    {
        var playerPosition = player.AbsOrigin;
        if (playerPosition == null)
            return false;

        List<CBaseEntity> enemySpawns = player.Team == CsTeam.CounterTerrorist ? ttSpawns : ctSpawns;

        foreach (var spawn in enemySpawns)
        {
            if (!spawn.IsValid || spawn.AbsOrigin == null)
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

        if (teamSpawns.Count == 0)
        {
            PrintDebugMessage("No spawn points available for team.");
            return;
        }

        var occupiedPositions = Utilities.GetPlayers()
            .Where(p => p.PawnIsAlive)
            .Select(p => p.AbsOrigin)
            .Where(pos => pos != null)
            .ToList();

        const float occupiedThreshold = 1.0f;

        foreach (var spawn in teamSpawns)
        {
            if (!spawn.IsValid || spawn.AbsOrigin == null)
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
            Server.PrintToChatAll(StringExtensions.ReplaceColorTags(string.Format(Localizer[keyValue], Config.PluginSettings.PluginTag)));
        }
    }

    private static bool ChangePlayerTeam(ulong steamId, CsTeam newTeam)
    {
        var player = Utilities.GetPlayerFromSteamId(steamId);
        if (player == null)
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

    public static void ValidatePlayerModel(CCSPlayerController player)
    {
        if (player == null || !player.IsValid) return;

        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null || !playerPawn.IsValid || playerPawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;

        string modelPath = playerPawn.CBodyComponent?.SceneNode?.GetSkeletonInstance().ModelState.ModelName ?? string.Empty;

        var team = player.Team;
        switch (team)
        {
            case CsTeam.CounterTerrorist when modelPath.Contains("/tm_"):
                playerPawn.SetModel("characters/models/ctm_sas/ctm_sas.vmdl");
                break;
            case CsTeam.Terrorist when modelPath.Contains("/ctm_"):
                playerPawn.SetModel("characters/models/tm_phoenix/tm_phoenix.vmdl");
                break;
        }
    }
}
