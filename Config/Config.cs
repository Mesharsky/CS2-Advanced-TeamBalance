using Tomlyn;
using Tomlyn.Model;
using System.Text;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance
{
    public static BalanceConfig? Config { get; private set; }

    public void LoadConfiguration()
    {
        var configPath = Path.Combine(ModuleDirectory, "TeamBalance.toml");

        if (!File.Exists(configPath))
        {
            GenerateDefaultConfigFile(configPath);
        }

        var configText = File.ReadAllText(configPath);
        var model = Toml.ToModel(configText);

        if (model.TryGetValue("PluginSettings", out var pluginSettingsObj) && pluginSettingsObj is TomlTable pluginTable)
        {
            var pluginTag = pluginTable["plugin_chat_tag"]?.ToString() ?? "[red][TeamBalance][default]";
            var minPlayers = int.Parse(pluginTable["minimum_players"]?.ToString() ?? "4");
            var maxScoreBalanceRatio = float.Parse(pluginTable["score_balance_ratio"]?.ToString() ?? "2.0");
            var usePerformanceScore = bool.Parse(pluginTable["use_performance_score"]?.ToString() ?? "true");
            var maxTeamSizeDifference = int.Parse(pluginTable["max_team_size_difference"]?.ToString() ?? "1");
            var enableDebugMessages = bool.Parse(pluginTable["enable_debug_messages"]?.ToString() ?? "true");
            var enableChatMessages = bool.Parse(pluginTable["enable_chat_messages"]?.ToString() ?? "true");
            var scrambleMode = pluginTable["scramble_mode"]?.ToString() ?? "none";
            var roundScrambleInterval = int.Parse(pluginTable["round_scramble_interval"]?.ToString() ?? "5");
            var winstreakScrambleThreshold = int.Parse(pluginTable["winstreak_scramble_threshold"]?.ToString() ?? "3");
            var halftimeScrambleEnabled = bool.Parse(pluginTable["halftime_scramble_enabled"]?.ToString() ?? "false");

            var pluginSettings = new PluginSettingsConfig
            {
                PluginTag = pluginTag,
                MinPlayers = minPlayers,
                MaxScoreBalanceRatio = maxScoreBalanceRatio,
                UsePerformanceScore = usePerformanceScore,
                MaxTeamSizeDifference = maxTeamSizeDifference,
                EnableDebugMessages = enableDebugMessages,
                EnableChatMessages = enableChatMessages,
                ScrambleMode = scrambleMode,
                RoundScrambleInterval = roundScrambleInterval,
                WinstreakScrambleThreshold = winstreakScrambleThreshold,
                HalftimeScrambleEnabled = halftimeScrambleEnabled
            };

            // Validate settings after loading
            pluginSettings.ValidateSettings();

            Config = new BalanceConfig
            {
                PluginSettings = pluginSettings
            };

            PrintDebugMessage("Configuration loaded successfully.");
        }
        else
        {
            PrintDebugMessage("'PluginSettings' section is missing in the configuration file.");
        }
    }

    private static void GenerateDefaultConfigFile(string configPath)
    {
        PrintDebugMessage("Configuration file not found. Generating default configuration...");

        var defaultConfig = new StringBuilder();

        defaultConfig.AppendLine("# Plugin Author - Mesharsky")
                    .AppendLine("# https://csowicze.pl/")
                    .AppendLine()
                    .AppendLine("# Team Balance Plugin Configuration")
                    .AppendLine("# Adjust these settings according to your server's needs.")
                    .AppendLine()
                    .AppendLine("[PluginSettings]")
                    .AppendLine("# Plugin's chat tag for messages in chat.")
                    .AppendLine("# Supported colors:")
                    .AppendLine("# [white], [darkred], [green], [lightyellow], [lightblue], [olive], [lime],")
                    .AppendLine("# [red], [lightpurple], [purple], [grey], [yellow], [gold], [silver], [blue],")
                    .AppendLine("# [darkblue], [bluegrey], [magenta], [lightred], [orange]")
                    .AppendLine("plugin_chat_tag = \"[red][TeamBalance]\"")
                    .AppendLine()
                    .AppendLine("# Minimum number of players required on the server before team balance activates.")
                    .AppendLine("# Prevents balancing when there are too few players.")
                    .AppendLine("# Default: 4")
                    .AppendLine("minimum_players = 4")
                    .AppendLine()
                    .AppendLine("# Maximum allowed score ratio between teams before triggering a balance.")
                    .AppendLine("# For example, if set to 2.0, balancing will trigger if one team's score")
                    .AppendLine("# is 2 times greater than the other team's score.")
                    .AppendLine("# Default: 2.0")
                    .AppendLine("score_balance_ratio = 2.0")
                    .AppendLine()
                    .AppendLine("# Use PerformanceScore for balancing.")
                    .AppendLine("# PerformanceScore takes into account KDA (Kills, Deaths, Assists),")
                    .AppendLine("# damage dealt, and the in-game score to evaluate player performance.")
                    .AppendLine("# Default: true")
                    .AppendLine("use_performance_score = true")
                    .AppendLine()
                    .AppendLine("# Maximum allowed difference in team sizes after balancing.")
                    .AppendLine("# If set to 1, the teams will be balanced such that one team cannot have")
                    .AppendLine("# more than one extra player compared to the other.")
                    .AppendLine("# Default: 1")
                    .AppendLine("max_team_size_difference = 1")
                    .AppendLine()
                    .AppendLine("# Enable or disable debug messages.")
                    .AppendLine("# If true, debug messages will be printed to the server console.")
                    .AppendLine("# Default: true")
                    .AppendLine("enable_debug_messages = true")
                    .AppendLine()
                    .AppendLine("# Enable or disable chat messages.")
                    .AppendLine("# If true, messages will be sent to the chat.")
                    .AppendLine("# Default: true")
                    .AppendLine("enable_chat_messages = true")
                    .AppendLine()
                    .AppendLine("# Scramble Mode Configuration")
                    .AppendLine("# scramble_mode determines when and how teams are scrambled.")
                    .AppendLine("# Options: \"none\" (no scrambling), \"round\" (scramble every X rounds),")
                    .AppendLine("# \"winstreak\" (scramble after a team wins X rounds in a row),")
                    .AppendLine("# \"halftime\" (scramble at halftime).")
                    .AppendLine("# Default: \"none\"")
                    .AppendLine("scramble_mode = \"none\"")
                    .AppendLine()
                    .AppendLine("# Number of rounds after which teams should be scrambled (if scramble_mode is \"round\").")
                    .AppendLine("# Default: 5")
                    .AppendLine("round_scramble_interval = 5")
                    .AppendLine()
                    .AppendLine("# Number of consecutive wins to trigger a scramble (if scramble_mode is \"winstreak\").")
                    .AppendLine("# Default: 3")
                    .AppendLine("winstreak_scramble_threshold = 3")
                    .AppendLine()
                    .AppendLine("# Enable or disable halftime scrambling (if scramble_mode is \"halftime\").")
                    .AppendLine("# Default: false")
                    .AppendLine("halftime_scramble_enabled = false");

        File.WriteAllText(configPath, defaultConfig.ToString());

        PrintDebugMessage("Default configuration file created successfully.");
    }
}
