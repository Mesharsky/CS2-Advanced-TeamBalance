using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Admin;
using Microsoft.Extensions.Localization;
using System.Linq;

namespace AdvancedTeamBalance
{
    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {
        public override string ModuleName => "Advanced Team Balance";
        public override string ModuleAuthor => "Mesharsky";
        public override string ModuleDescription => "Provides advanced team balancing for CS2 servers";
        public override string ModuleVersion => "5.2.0";

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
            ValidateConfiguration();
            PlayerManager.Initialize(Config);
            BalanceManager.Initialize(Config);
            EventManager.Initialize(Config);
            Console.WriteLine("[AdvancedTeamBalance] Configuration parsed and managers updated.");
        }
        
        private void ValidateConfiguration()
        {
            bool hasErrors = false;
            
            // Validate MinimumPlayers
            if (Config.General.MinimumPlayers < 0)
            {
                Console.WriteLine("[AdvancedTeamBalance] WARNING: MinimumPlayers cannot be negative. Setting to 0.");
                Config.General.MinimumPlayers = 0;
                hasErrors = true;
            }
            
            // Validate MaxTeamSizeDifference
            if (Config.TeamSwitch.MaxTeamSizeDifference < 0)
            {
                Console.WriteLine("[AdvancedTeamBalance] WARNING: MaxTeamSizeDifference cannot be negative. Setting to 1.");
                Config.TeamSwitch.MaxTeamSizeDifference = 1;
                hasErrors = true;
            }
            
            // Validate MinRoundsBeforeSwitch
            if (Config.TeamSwitch.MinRoundsBeforeSwitch < 0)
            {
                Console.WriteLine("[AdvancedTeamBalance] WARNING: MinRoundsBeforeSwitch cannot be negative. Setting to 0.");
                Config.TeamSwitch.MinRoundsBeforeSwitch = 0;
                hasErrors = true;
            }
            
            // Validate BalanceMode
            var validModes = new[] { "KD", "KDA", "Score", "WinRate", "ScrambleRandom", "ScrambleSkill" };
            if (!validModes.Any(m => m.Equals(Config.Balancing.BalanceMode, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"[AdvancedTeamBalance] WARNING: Invalid BalanceMode '{Config.Balancing.BalanceMode}'. Setting to 'KDA'.");
                Config.Balancing.BalanceMode = "KDA";
                hasErrors = true;
            }
            
            // Validate SkillDifferenceThreshold
            if (Config.Balancing.SkillDifferenceThreshold < 0.0 || Config.Balancing.SkillDifferenceThreshold > 1.0)
            {
                Console.WriteLine($"[AdvancedTeamBalance] WARNING: SkillDifferenceThreshold must be between 0.0 and 1.0. Setting to 0.2.");
                Config.Balancing.SkillDifferenceThreshold = 0.2;
                hasErrors = true;
            }
            
            // Validate BoostPercentage
            if (Config.Balancing.BoostPercentage < 0 || Config.Balancing.BoostPercentage > 100)
            {
                Console.WriteLine($"[AdvancedTeamBalance] WARNING: BoostPercentage must be between 0 and 100. Setting to 20.");
                Config.Balancing.BoostPercentage = 20;
                hasErrors = true;
            }
            
            // Validate BoostTiers if Progressive Boost is enabled
            if (Config.Balancing.ProgressiveBoost && Config.Balancing.BoostTiers != null)
            {
                foreach (var tier in Config.Balancing.BoostTiers.ToList())
                {
                    if (tier.Key < 0 || tier.Value < 0 || tier.Value > 100)
                    {
                        Console.WriteLine($"[AdvancedTeamBalance] WARNING: Invalid BoostTier [{tier.Key}:{tier.Value}]. Removing.");
                        Config.Balancing.BoostTiers.Remove(tier.Key);
                        hasErrors = true;
                    }
                }
            }
            
            // Validate BalanceTriggers
            var validTriggers = new[] { "OnRoundStart", "OnRoundEnd", "OnPlayerJoin", "OnPlayerDisconnect", "OnFreezeTimeEnd" };
            var invalidTriggers = Config.TeamSwitch.BalanceTriggers.Where(t => !validTriggers.Contains(t)).ToList();
            if (invalidTriggers.Any())
            {
                Console.WriteLine($"[AdvancedTeamBalance] WARNING: Invalid BalanceTriggers found: {string.Join(", ", invalidTriggers)}. Removing.");
                Config.TeamSwitch.BalanceTriggers = Config.TeamSwitch.BalanceTriggers.Where(t => validTriggers.Contains(t)).ToList();
                hasErrors = true;
            }
            
            if (!hasErrors && Config.General.EnableDebug)
            {
                Console.WriteLine("[AdvancedTeamBalance] Configuration validation passed.");
            }
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
            
            // Register player commands
            AddCommand("css_statsssssss", "Display your balance statistics", CommandStats);
            AddCommand("css_mystatssssss", "Display your balance statistics", CommandStats);
            
            // Register admin commands
            AddCommand("css_balancepreview", "Preview the next balance operation", CommandPreviewBalance);
            AddCommand("css_previewbalance", "Preview the next balance operation", CommandPreviewBalance);
        }
        
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        private void CommandStats(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid || player.IsBot)
                return;
            
            var playerData = PlayerManager.GetOrAddPlayer(player);
            
            ChatHelper.PrintLocalizedChat(player, true, "stats.header");
            ChatHelper.PrintLocalizedChat(player, true, "stats.kd", playerData.Stats.KDRatio.ToString("F2"));
            ChatHelper.PrintLocalizedChat(player, true, "stats.kda", playerData.Stats.KDARatio.ToString("F2"));
            ChatHelper.PrintLocalizedChat(player, true, "stats.score", playerData.Stats.Score);
            ChatHelper.PrintLocalizedChat(player, true, "stats.winrate", (playerData.Stats.WinRate * 100).ToString("F1"));
            ChatHelper.PrintLocalizedChat(player, true, "stats.rounds", playerData.Stats.RoundsPlayed);
            ChatHelper.PrintLocalizedChat(player, true, "stats.switches", playerData.TimesSwitched);
        }
        
        [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_ONLY)]
        private void CommandPreviewBalance(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid || player.IsBot)
                return;
            
            // Check if player has admin privileges
            if (!player.PlayerPawn.IsValid)
                return;
            
            bool isAdmin = AdminManager.PlayerHasPermissions(player, Config.Admin.AdminExemptFlag);
            
            if (!isAdmin)
            {
                player.PrintToChat($"{Config.General.PluginTag} You don't have permission to use this command.");
                return;
            }
            
            // Get current team states
            var tPlayers = PlayerManager.GetPlayersByTeam(CsTeam.Terrorist);
            var ctPlayers = PlayerManager.GetPlayersByTeam(CsTeam.CounterTerrorist);
            
            var tPlayersCopy = new List<Player>(tPlayers);
            var ctPlayersCopy = new List<Player>(ctPlayers);
            
            // Calculate current averages
            double currentTAvg = tPlayers.Count > 0 ? tPlayers.Average(p => BalanceManager.GetPlayerValuePublic(p, Config.Balancing.BalanceMode)) : 0;
            double currentCTAvg = ctPlayers.Count > 0 ? ctPlayers.Average(p => BalanceManager.GetPlayerValuePublic(p, Config.Balancing.BalanceMode)) : 0;
            
            // Simulate balance
            var result = BalanceManager.BalanceTeams(tPlayersCopy, ctPlayersCopy, 0, 0, false);
            
            ChatHelper.PrintLocalizedChat(player, true, "preview.header");
            ChatHelper.PrintLocalizedChat(player, true, "preview.current", 
                tPlayers.Count, currentTAvg.ToString("F2"), 
                ctPlayers.Count, currentCTAvg.ToString("F2"));
            ChatHelper.PrintLocalizedChat(player, true, "preview.proposed", 
                tPlayersCopy.Count, ctPlayersCopy.Count);
            ChatHelper.PrintLocalizedChat(player, true, "preview.moves", 
                result.PlayersMoved, result.SwapsMade);
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
