<<<<<<< HEAD
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Localization;

namespace AdvancedTeamBalance
{
    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        public override string ModuleName => "Advanced Team Balance";
        public override string ModuleAuthor => "Mesharsky";
        public override string ModuleDescription => "Provides advanced team balancing for CS2 servers";
        public override string ModuleVersion => "5.0.1";

        public PluginConfig Config { get; set; } = new();

        public static Plugin? Instance { get; private set; }

        public static IStringLocalizer? _localizer;

        public override void Load(bool hotReload)
        {
            Instance = this;

            PlayerManager.Initialize(Config);
            BalanceManager.Initialize(Config);
            EventManager.Initialize(Config);

            RegisterEventHandlers();
            RegisterCommandHandlers();

            Console.WriteLine("[AdvancedTeamBalance] Plugin loaded successfully!");

            if (hotReload)
            {
                PlayerManager.Initialize(Config);
                BalanceManager.Initialize(Config);
                EventManager.Initialize(Config);
                 Console.WriteLine("[AdvancedTeamBalance] Hot reload: Managers re-initialized.");
            }
        }

        public override void OnAllPluginsLoaded(bool isReload)
        {
            _localizer = Localizer;
        }

        public void OnConfigParsed(PluginConfig config)
        {
            Config = config;
            PlayerManager.Initialize(Config);
            BalanceManager.Initialize(Config);
            EventManager.Initialize(Config);
            Console.WriteLine("[AdvancedTeamBalance] Configuration parsed and managers updated.");
        }

        private void RegisterEventHandlers()
        {
            RegisterEventHandler<EventPlayerConnectFull>(EventManager.OnPlayerConnectFull);
            RegisterEventHandler<EventPlayerDisconnect>(EventManager.OnPlayerDisconnect);
            RegisterEventHandler<EventPlayerDeath>(EventManager.OnPlayerDeath);
            RegisterEventHandler<EventRoundEnd>(EventManager.OnRoundEnd);
            RegisterEventHandler<EventPlayerSpawn>(EventManager.OnPlayerSpawn);
            RegisterEventHandler<EventRoundPrestart>(EventManager.OnRoundStart);

            RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        }

        private void RegisterCommandHandlers()
        {
            AddCommandListener("jointeam", CommandJoinTeam, HookMode.Pre);
        }

        private void OnMapEnd()
        {
            if (Config.General.EnableDebug)
            {
                Console.WriteLine("[AdvancedTeamBalance] Map ended, resetting all statistics and counters");
            }

            EventManager.ResetMapStats();
            PlayerManager.ResetAllPlayerStats();

            if (Config.General.EnableDebug)
            {
                Console.WriteLine("[AdvancedTeamBalance] All player statistics and counters have been reset");
            }
        }

        private HookResult CommandJoinTeam(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid || player.IsBot)
                return HookResult.Continue;

            if (!int.TryParse(info.GetArg(1), out int teamIdArg))
                 return HookResult.Continue;

            CsTeam desiredTeamEnum = (CsTeam)teamIdArg;


            if (desiredTeamEnum == CsTeam.Spectator)
                return HookResult.Continue;

            if (desiredTeamEnum != CsTeam.Terrorist && desiredTeamEnum != CsTeam.CounterTerrorist)
                return HookResult.Continue;

            var playerData = PlayerManager.GetOrAddPlayer(player);
            CsTeam currentActualPlayerTeam = (CsTeam)player.TeamNum;

            if (currentActualPlayerTeam == desiredTeamEnum)
                 return HookResult.Continue;

            if (playerData.IsExemptFromSwitching)
                 return HookResult.Continue;

            var (tCount, ctCount) = PlayerManager.GetTeamCounts();

            int newTCount = tCount;
            int newCTCount = ctCount;
            bool isPlayerSwitchingTeams = false;

            if (currentActualPlayerTeam == CsTeam.Terrorist)
            {
                newTCount--;
                isPlayerSwitchingTeams = true;
            }
            else if (currentActualPlayerTeam == CsTeam.CounterTerrorist)
            {
                newCTCount--;
                isPlayerSwitchingTeams = true;
            }

            if (desiredTeamEnum == CsTeam.Terrorist)
                newTCount++;
            else if (desiredTeamEnum == CsTeam.CounterTerrorist)
                newCTCount++;
            
            int potentialDifferenceAfterJoin = Math.Abs(newTCount - newCTCount);
            bool allowJoinOperation = false;

            if (!isPlayerSwitchingTeams && Config.TeamSwitch.MaxTeamSizeDifference == 0 && potentialDifferenceAfterJoin == 1)
            {
                allowJoinOperation = true;
                if (Config.General.EnableDebug)
                {
                    Console.WriteLine($"[AdvancedTeamBalance] JoinTeam CMD: Permitting non-switcher {player.PlayerName} to join {desiredTeamEnum} (making teams {newTCount}v{newCTCount}) despite MaxTeamSizeDifference=0, because potential difference is 1.");
                }
            }
            else if (potentialDifferenceAfterJoin <= Config.TeamSwitch.MaxTeamSizeDifference)
            {
                allowJoinOperation = true;
            }

            if (allowJoinOperation)
            {
                playerData.UpdateTeamState(desiredTeamEnum, 0);
                if (Config.General.EnableDebug)
                {
                     Console.WriteLine($"[AdvancedTeamBalance] JoinTeam CMD: Allowing {player.PlayerName} to join {desiredTeamEnum}. Teams will be T:{newTCount} CT:{newCTCount}. PotentialDiff: {potentialDifferenceAfterJoin} <= MaxDiff: {Config.TeamSwitch.MaxTeamSizeDifference}");
                }
                return HookResult.Continue;
            }
            else
            {
                // Determine the correct team for balance
                CsTeam correctTeam;
                if (newTCount > newCTCount)
                    correctTeam = CsTeam.CounterTerrorist;
                else if (newCTCount > newTCount)
                    correctTeam = CsTeam.Terrorist;
                else
                {
                    correctTeam = (desiredTeamEnum == CsTeam.Terrorist) ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
                }

                if (correctTeam != CsTeam.None && currentActualPlayerTeam != correctTeam)
                {
                    if (player.PawnIsAlive)
                    {
                        ChatHelper.PrintLocalizedChat(player, true, "jointeam.imbalance");
                        ChatHelper.PrintLocalizedChat(player, true, "jointeam.delayed");
                        playerData.UpdateTeamState(correctTeam, 0);
                    }
                    else
                    {
                        player.ChangeTeam(correctTeam);
                        playerData.UpdateTeamState(correctTeam, 0);
                        ChatHelper.PrintLocalizedChat(player, true, "jointeam.forced", correctTeam.ToString());
                    }
                }
                else if (correctTeam == currentActualPlayerTeam)
                {
                    ChatHelper.PrintLocalizedChat(player, true, "jointeam.already_balanced");
                }

                if (Config.General.EnableDebug)
                {
                     Console.WriteLine($"[AdvancedTeamBalance] JoinTeam CMD: Auto-switching {player.PlayerName} from {desiredTeamEnum} to {correctTeam} for balance. Teams would be T:{newTCount} CT:{newCTCount}. PotentialDiff: {potentialDifferenceAfterJoin} > MaxDiff: {Config.TeamSwitch.MaxTeamSizeDifference}. Alive: {player.PawnIsAlive}");
                }
                return HookResult.Handled;
            }
        }

        public override void Unload(bool hotReload)
        {
            PlayerManager.Cleanup();
            Instance = null;
            _localizer = null;

            Console.WriteLine("[AdvancedTeamBalance] Plugin unloaded");
        }
    }
}
=======
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Localization;

namespace AdvancedTeamBalance
{
    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        public override string ModuleName => "Advanced Team Balance";
        public override string ModuleAuthor => "Mesharsky";
        public override string ModuleDescription => "Provides advanced team balancing for CS2 servers";
        public override string ModuleVersion => "5.0.1";
        
        public PluginConfig Config { get; set; } = new();
        
        public static Plugin? Instance { get; private set; }
        
        public static IStringLocalizer? _localizer;
        
        public override void Load(bool hotReload)
        {
            Instance = this;
            
            PlayerManager.Initialize(Config);
            BalanceManager.Initialize(Config);
            EventManager.Initialize(Config);
            
            RegisterEventHandlers();
            
            RegisterCommandHandlers();
            
            Console.WriteLine("[AdvancedTeamBalance] Plugin loaded successfully!");

            if (hotReload)
            {
                PlayerManager.Initialize(Config);
                BalanceManager.Initialize(Config);
                EventManager.Initialize(Config);
            }
        }
        
        public override void OnAllPluginsLoaded(bool isReload)
        {
            _localizer = Localizer;
        }
        
        public void OnConfigParsed(PluginConfig config)
        {
            Config = config;

            // Just in case...
            PlayerManager.Initialize(Config);
            BalanceManager.Initialize(Config);
            EventManager.Initialize(Config);
        }
        
        private void RegisterEventHandlers()
        {
            RegisterEventHandler<EventPlayerConnectFull>(EventManager.OnPlayerConnectFull);
            RegisterEventHandler<EventPlayerDisconnect>(EventManager.OnPlayerDisconnect);
            RegisterEventHandler<EventPlayerDeath>(EventManager.OnPlayerDeath);
            RegisterEventHandler<EventRoundPrestart>((@event, info) => 
            {
                EventManager.SetRoundPreStartPhase(true);
                
                var result = EventManager.OnRoundStart(@event, info);
                
                EventManager.SetRoundPreStartPhase(false);
                
                return result;
            });
            RegisterEventHandler<EventRoundEnd>(EventManager.OnRoundEnd);
            RegisterEventHandler<EventPlayerSpawn>(EventManager.OnPlayerSpawn);

            RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        }
        
        private void RegisterCommandHandlers()
        {
            AddCommandListener("jointeam", CommandJoinTeam);
        }

        private void OnMapEnd()
        {
            if (Config.General.EnableDebug)
            {
                Console.WriteLine("[AdvancedTeamBalance] Map ended, resetting all statistics and counters");
            }
            
            EventManager.ResetMapStats();
            PlayerManager.ResetAllPlayerStats();
            
            if (Config.General.EnableDebug)
            {
                Console.WriteLine("[AdvancedTeamBalance] All player statistics and counters have been reset");
            }
        }

        private HookResult CommandJoinTeam(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid || player.IsBot)
                return HookResult.Continue;
            
            if (!int.TryParse(info.GetArg(1), out int teamId))
                return HookResult.Continue;
            
            if (teamId == (int)CsTeam.Spectator)
                return HookResult.Continue;
            
            if (teamId != (int)CsTeam.Terrorist && teamId != (int)CsTeam.CounterTerrorist)
                return HookResult.Continue;
            
            var playerData = PlayerManager.GetOrAddPlayer(player);
            
            if ((int)playerData.Team == teamId)
                return HookResult.Continue;
            
            bool isExempt = playerData.IsExemptFromSwitching;
            if (isExempt)
                return HookResult.Continue;
            
            var (tCount, ctCount) = PlayerManager.GetTeamCounts();
            
            int newTCount = tCount;
            int newCTCount = ctCount;
            
            if (playerData.Team == CsTeam.Terrorist)
                newTCount--;
            else if (playerData.Team == CsTeam.CounterTerrorist)
                newCTCount--;
            
            if (teamId == (int)CsTeam.Terrorist)
                newTCount++;
            else if (teamId == (int)CsTeam.CounterTerrorist)
                newCTCount++;
            
            int potentialDifference = Math.Abs(newTCount - newCTCount);
            
            if (potentialDifference > Config.TeamSwitch.MaxTeamSizeDifference)
            {
                ChatHelper.PrintLocalizedChat(player, true, "jointeam.imbalance");
                
                CsTeam suggestedTeam = teamId == (int)CsTeam.Terrorist ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
                ChatHelper.PrintLocalizedChat(player, true, "jointeam.suggest", suggestedTeam.ToString());
                
                if (playerData.Team != CsTeam.None && playerData.Team != CsTeam.Spectator)
                {
                    if (!player.PawnIsAlive)
                    {
                        player.SwitchTeam(suggestedTeam);
                        playerData.UpdateTeamState(suggestedTeam, 0);
                        ChatHelper.PrintLocalizedChat(player, true, "jointeam.forced", suggestedTeam.ToString());
                    }
                    else
                    {
                        ChatHelper.PrintLocalizedChat(player, true, "jointeam.delayed");
                    }
                }
                
                return HookResult.Handled;
            }
            
            playerData.UpdateTeamState((CsTeam)teamId, 0);
            
            return HookResult.Continue;
        }

        public override void Unload(bool hotReload)
        {
            PlayerManager.Cleanup();
            Instance = null;
            
            Console.WriteLine("[AdvancedTeamBalance] Plugin unloaded");
        }
    }
}
>>>>>>> 74fc461956bce7b38451c220f391a210b6af4d41
