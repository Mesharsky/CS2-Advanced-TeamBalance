using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;

namespace AdvancedTeamBalance
{
    public static class ChatHelper
    {
        /// <summary>
        /// Prints a localized message to the player's chat.
        /// </summary>
        /// <param name="player">The CCSPlayerController to print to.</param>
        /// <param name="includePrefix">
        /// If true, the plugin prefix (PluginTag) is included as the first argument.
        /// Translation strings will expect {0} for the prefix.
        /// </param>
        /// <param name="key">The translation key (as defined in your language JSON file).</param>
        /// <param name="args">Any additional arguments to format the string.</param>
        public static void PrintLocalizedChat(CCSPlayerController player, bool includePrefix, string key, params object[] args)
        {
            if (!player.IsValid)
                return;

            if (Plugin._localizer == null)
            {
                Console.WriteLine($"[AdvancedTeamBalance][ChatHelper] ERROR: _localizer is NULL. Key: {key}");
                return;
            }

            var sanitizedArgs = args.Select(arg => arg).ToArray();

            if (includePrefix)
            {
                var prefix = Plugin.Instance?.Config.General.PluginTag.ReplaceColorTags();
                sanitizedArgs = [prefix, .. sanitizedArgs];
            }

            try
            {
                var formattedMessage = Plugin._localizer.ForPlayer(player, key, sanitizedArgs);
                player.PrintToChat(formattedMessage);
            }
            catch (FormatException fe)
            {
                Console.WriteLine($"[AdvancedTeamBalance][ChatHelper] ERROR: Formatting failed for key: {key} | Args: {string.Join(", ", sanitizedArgs)}");
                Console.WriteLine($"[AdvancedTeamBalance][ChatHelper] Exception: {fe.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdvancedTeamBalance][ChatHelper] ERROR: Unexpected error for key: {key}");
                Console.WriteLine($"[AdvancedTeamBalance][ChatHelper] Exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Prints a localized message to all players' chat.
        /// </summary>
        /// <param name="includePrefix">
        /// If true, the plugin prefix (PluginTag) is included as the first argument.
        /// Translation strings will expect {0} for the prefix.
        /// </param>
        /// <param name="key">The translation key (as defined in your language JSON file).</param>
        /// <param name="args">Any additional arguments to format the string.</param>
        public static void PrintLocalizedChatAll(bool includePrefix, string key, params object[] args)
        {
            if (Plugin._localizer == null)
            {
                Console.WriteLine($"[AdvancedTeamBalance][ChatHelper] ERROR: _localizer is NULL. Key: {key}");
                return;
            }

            var players = Utilities.GetPlayers();
            if (players.Count == 0)
                return;

            foreach (var player in players)
            {
                if (player == null || !player.IsValid)
                    continue;

                PrintLocalizedChat(player, includePrefix, key, args);
            }
        }
    }
}
