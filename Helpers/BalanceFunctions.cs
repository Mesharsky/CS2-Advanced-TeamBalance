using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    private void AttemptBalanceTeams()
    {
        PrintDebugMessage("Attempting to balance teams...");

        if (!ShouldTeamsBeRebalanced())
            return;

        PrintDebugMessage("Balancing teams...");

        var players = GetPlayersForRebalance();
        bool balanceMade = RebalancePlayers(players);

        if (balanceMade)
        {
            BalanceHasBeenMade = true;
            Server.PrintToChatAll($"{ChatColors.Red}[Balans Drużyn] {ChatColors.Default}Drużyny zostały zbalansowane");
        }
        else
        {
            BalanceHasBeenMade = false;
        }
    }

    private static List<Player> GetPlayersForRebalance()
    {
        var players = playerCache.Values
            .Where(p => p.Team == (int)CsTeam.CounterTerrorist || p.Team == (int)CsTeam.Terrorist)
            .OrderBy(x => Guid.NewGuid())
            .OrderByDescending(p => p.Score)
            .ToList();

        PrintDebugMessage($"Total valid players for rebalance: {players.Count}");
        return players;
    }

    private static bool RebalancePlayers(List<Player> players)
    {
        PrintDebugMessage("Starting player rebalance...");

        int totalPlayers = players.Count;
        int maxPerTeam = totalPlayers / 2 + (totalPlayers % 2);

        List<Player> ctTeam = new List<Player>();
        List<Player> tTeam = new List<Player>();

        int ctTotalScore = 0;
        int tTotalScore = 0;

        bool balanceMade = false;

        PrintDebugMessage($"RebalancePlayers: totalPlayers={totalPlayers}, maxPerTeam={maxPerTeam}");
        
        foreach (var player in players)
        {
            bool ctValidChoice = (tTeam.Count >= maxPerTeam || ctTotalScore <= tTotalScore) && ctTeam.Count < maxPerTeam;
            bool tValidChoice = (ctTeam.Count >= maxPerTeam || tTotalScore <= ctTotalScore) && tTeam.Count < maxPerTeam;

            if (ctValidChoice && player.Team != (int)CsTeam.CounterTerrorist)
            {
                PrintDebugMessage($"Move {player.PlayerName} to CT (ctTotal={ctTotalScore}, ctCount={ctTeam.Count + 1})");
                ChangePlayerTeam(player.PlayerSteamID, CsTeam.CounterTerrorist);

                ctTeam.Add(player);
                ctTotalScore += player.Score;
                balanceMade = true;
            }
            else if (tValidChoice && player.Team != (int)CsTeam.Terrorist)
            {
                PrintDebugMessage($"Move {player.PlayerName} to T (tTotal={tTotalScore}, tCount={tTeam.Count + 1})");
                ChangePlayerTeam(player.PlayerSteamID, CsTeam.Terrorist);
                Server.PrintToChatAll($"{ChatColors.Green}{player.PlayerName} {ChatColors.Default} został przeniesiony do {ChatColors.Red}Terrorists{ChatColors.Default} aby wyrównać drużyny.");
                tTeam.Add(player);
                tTotalScore += player.Score;
                balanceMade = true;
            }
            else
            {
                if (player.Team == (int)CsTeam.CounterTerrorist)
                {
                    ctTeam.Add(player);
                    ctTotalScore += player.Score;
                }
                else if (player.Team == (int)CsTeam.Terrorist)
                {
                    tTeam.Add(player);
                    tTotalScore += player.Score;
                }
            }
        }

        PrintDebugMessage($"Final Team Distribution - CT: {ctTeam.Count} players, T: {tTeam.Count} players");

        return balanceMade;
    }

    private static bool ShouldTeamsBeRebalanced()
    {
        PrintDebugMessage("Evaluating if teams need to be rebalanced...");

        UpdatePlayerTeamsInCache();

        var players = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && !p.IsBot && !p.IsHLTV && p.Connected == PlayerConnectedState.PlayerConnected)
            .ToList();

        int ctPlayerCount = players.Count(p => p.Team == CsTeam.CounterTerrorist);
        int tPlayerCount = players.Count(p => p.Team == CsTeam.Terrorist);

        if (ctPlayerCount + tPlayerCount < Config?.PluginSettings.MinPlayers)
        {
            PrintDebugMessage("Not enough players to balance.");
            return false;
        }

        int ctScore = players.Where(p => p.Team == CsTeam.CounterTerrorist).Sum(p => p.Score);
        int tScore = players.Where(p => p.Team == CsTeam.Terrorist).Sum(p => p.Score);

        if (ctScore > tScore * Config?.PluginSettings.MaxScoreBalanceRatio || tScore > ctScore * Config?.PluginSettings.MaxScoreBalanceRatio)
        {
            PrintDebugMessage("Score difference is too high. Balance required.");
            return true;
        }

        if (Math.Abs(ctPlayerCount - tPlayerCount) > 1)
        {
            PrintDebugMessage("Team sizes are not equal. Balance needed.");
            return true;
        }

        PrintDebugMessage("No balance required. Teams are balanced.");
        return false;
    }
}
