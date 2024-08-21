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
            var minPlayers = int.Parse(pluginTable["minimum_players"]?.ToString() ?? "4");
            var maxScoreBalanceRatio = float.Parse(pluginTable["score_balance_ratio"]?.ToString() ?? "1.6");

            var pluginSettings = new PluginSettingsConfig
            {
                MinPlayers = minPlayers,
                MaxScoreBalanceRatio = maxScoreBalanceRatio
            };

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

        defaultConfig.AppendLine("# Team Balance Plugin Configuration")
                     .AppendLine("# Adjust these settings according to your server's needs.")
                     .AppendLine()
                     .AppendLine("[PluginSettings]")
                     .AppendLine("# The minimum number of players required on the server before the team balance")
                     .AppendLine("# feature activates. This prevents balancing when there are too few players,")
                     .AppendLine("# Default: 4")
                     .AppendLine("minimum_players = 4")
                     .AppendLine()
                     .AppendLine("# The maximum allowed ratio of scores between teams before triggering a balance.")
                     .AppendLine("# For example, if set to 1.6, the balance will trigger if one team's score is")
                     .AppendLine("# 60% higher than the other team's score. Adjust this value based on how strict")
                     .AppendLine("# you want the balancing to be.")
                     .AppendLine("# Default: 1.6")
                     .AppendLine("score_balance_ratio = 1.6");

        File.WriteAllText(configPath, defaultConfig.ToString());

        PrintDebugMessage("Default configuration file created successfully.");
    }
}
