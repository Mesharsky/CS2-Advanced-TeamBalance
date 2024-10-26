using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

namespace Mesharsky_TeamBalance;

public static class GameRules
{
    private static CCSGameRulesProxy? _gameRulesEntity = null;
    private static readonly ConVar mp_halftime = ConVar.Find("mp_halftime")!;
    private static readonly ConVar mp_maxrounds = ConVar.Find("mp_maxrounds")!;
    private static readonly ConVar mp_overtime_enable = ConVar.Find("mp_overtime_enable")!;

    private static int TotalRoundsPlayed => _gameRulesEntity?.GameRules?.TotalRoundsPlayed ?? 0;
    private static int MaxRounds => mp_maxrounds.GetPrimitiveValue<int>();
    private static bool HalfTime => mp_halftime.GetPrimitiveValue<bool>();
    private static bool OverTime => mp_overtime_enable.GetPrimitiveValue<bool>();

    private static void CheckGameRules()
    {
        if (_gameRulesEntity?.IsValid is not true)
        {
            _gameRulesEntity = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
        }
    }

    private static bool GameRestart()
    {
        CheckGameRules();

        return _gameRulesEntity?.GameRules?.GameRestart ?? false;
    }

    public static bool IsWarmup()
    {
        CheckGameRules();

        return _gameRulesEntity?.GameRules?.WarmupPeriod ?? false;
    }

    public static bool IsHalftime()
    {
        if (MaxRounds == 0 || GameRestart() || !HalfTime)
        {
            return false;
        }

        return TotalRoundsPlayed == MaxRounds / 2;
    }

    public static bool IsOvertime()
    {
        if (MaxRounds == 0 || !OverTime)
        {
            return false;
        }

        return GameRestart() || TotalRoundsPlayed >= MaxRounds;
    }

    public static bool IsNextRoundHalftime()
    {
        if (MaxRounds == 0 || !HalfTime)
        {
            return false;
        }

        return TotalRoundsPlayed + 1 == MaxRounds / 2;
    }

    public static bool IsNextRoundOvertime()
    {
        if (MaxRounds == 0 || !OverTime)
        {
            return false;
        }

        return GameRestart() || TotalRoundsPlayed + 1 > MaxRounds;
    }
}