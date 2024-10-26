# Mesharsky Advanced Team Balance Plugin

![Plugin Version](https://img.shields.io/badge/Plugin%20Version-3.0.0-blue)
![License](https://img.shields.io/badge/License-MIT-green)
![Supported Languages](https://img.shields.io/badge/Supported%20Languages-7-brightgreen)

Comprehensive solution designed to ensure fair and balanced gameplay on your CS2 Server. This plugin intelligently manages team sizes and player performance metrics to create a more competitive and enjoyable experience for all players.

## 🚀 Features

- **Intelligent Team Balancing**
  - Automatically balances teams based on player performance metrics (Kills, Deaths, Damage, KDA and Score).
  - Uses a custom `PerformanceScore` for more accurate balancing. (Can be disabled)
- **Configurable Settings**
  - Highly customizable via the configuration file.
  - Control team size differences, balancing thresholds, and more.
- **Scramble Mode Support**
  - Multiple scramble modes available: none, round-based, win streak, and halftime.
- **Halftime and Overtime Checks**
  - Additional support for halftime and overtime scenarios to prevent unfair advantages.
- **Spawn Correction**
  - Precisely checks player team and position on spawn.
  - Automatically teleports players to the correct spawn point if they spawn in the wrong location.
- **Translations**
  - Now supports multiple languages:
    - English
    - Polish
    - Spanish
    - Russian
    - French
    - Turkish
    - German

## 📥 Installation

1. **Download the Plugin**
   - Get the latest release from the [Releases Page](https://github.com/Mesharsky/Advanced-TeamBalance/releases).
2. **Upload to Your Server**
   - Place the plugin folder inside your `CounterStrikeSharp` directory.
3. **Configure the Plugin**
   - Edit the `TeamBalance.toml` file located in the module directory to adjust settings as needed.
4. **Restart Your Server**
   - Restart the server to load the plugin with your customized settings.

---

**Note:** Please replace your old configuration file with the new one provided in latest release

---

## ⚙️ Configuration

```
# Plugin Author - Mesharsky
# https://csowicze.pl/

# Team Balance Plugin Configuration
# Adjust these settings according to your server's needs.

[PluginSettings]

# Plugin's chat tag for messages in chat.
# Supported colors:
# [white], [darkred], [green], [lightyellow], [lightblue], [olive], [lime],
# [red], [lightpurple], [purple], [grey], [yellow], [gold], [silver], [blue],
# [darkblue], [bluegrey], [magenta], [lightred], [orange]
plugin_chat_tag = " [red][TeamBalance]"

# Minimum number of players required on the server before team balance activates.
# Prevents balancing when there are too few players.
# Default: 4
minimum_players = 4

# Maximum allowed score ratio between teams before triggering a balance.
# For example, if set to 2.0, balancing will trigger if one team's score
# is 2 times greater than the other team's score.
# Default: 2.0
score_balance_ratio = 2.0

# Use PerformanceScore for balancing.
# PerformanceScore takes into account KDA (Kills, Deaths, Assists),
# damage dealt, and the in-game score to evaluate player performance.
# Default: true
use_performance_score = true

# Maximum allowed difference in team sizes after balancing.
# If set to 1, the teams will be balanced such that one team cannot have
# more than one extra player compared to the other.
# Default: 1
max_team_size_difference = 1

# Enable or disable debug messages.
# If true, debug messages will be printed to the server console.
# Default: true
enable_debug_messages = true

# Enable or disable chat messages.
# If true, messages will be sent to the chat.
# Default: true
enable_chat_messages = true

# Scramble Mode Configuration
# scramble_mode determines when and how teams are scrambled.
# Options: "none" (no scrambling), "round" (scramble every X rounds),
# "winstreak" (scramble after a team wins X rounds in a row),
# "halftime" (scramble at halftime).
# Default: "none"
scramble_mode = "none"

# Number of rounds after which teams should be scrambled (if scramble_mode is "round").
# Default: 5
round_scramble_interval = 5

# Number of consecutive wins to trigger a scramble (if scramble_mode is "winstreak").
# Default: 3
winstreak_scramble_threshold = 3

# Enable or disable halftime scrambling (if scramble_mode is "halftime").
# Default: false
halftime_scramble_enabled = false
```

### Key Settings Explained

- **`minimum_players`**
  - Ensures balancing doesn't occur when there are too few players.
- **`score_balance_ratio`**
  - Triggers a rebalance if one team's score exceeds the other's by this ratio.
- **`use_performance_score`**
  - Uses a custom metric for more effective balancing.
- **`max_team_size_difference`**
  - Prevents significant numerical advantages by limiting team size differences.
- **`plugin_chat_tag`**
  - Customize the in-game chat tag for plugin messages with color support.
- **`scramble_mode`**
  - Automatically scramble teams based on specified conditions.

---

## 🌐 Translations

The plugin now supports multiple languages for in-game messages:

- **English**
- **Polish**
- **Spanish**
- **Russian**
- **French**
- **Turkish**
- **German**

---

## 🛠 How It Works

1. **Player Stats Collection**
   - Collects stats (Kills, Deaths, Damage, Score) and stores them in a cache.
2. **Balance Check**
   - Evaluates whether teams need rebalancing based on team sizes and score ratios.
3. **Team Balancing**
   - Distributes players to minimize score differences while respecting team size limits.
   - Moves only the necessary players to achieve balance.
4. **Spawn Correction**
   - Checks player positions on spawn.
   - Teleports players to the correct spawn point if they are in the wrong location.
5. **Feedback**
   - Notifies players via in-game chat when a balance or scramble occurs.

---

## 📊 Example Scenarios

### Scenario 1: Uneven Team Sizes

- **Situation:** 10 players on Terrorists, 1 player on Counter-Terrorists.
- **Action:** The plugin moves players from Terrorists to Counter-Terrorists until team sizes are balanced according to `max_team_size_difference`.

### Scenario 2: High Score Disparity

- **Situation:** Terrorists have a score 70% higher than Counter-Terrorists.
- **Action:** The plugin rebalances teams by moving top-performing players to even out the scores.

---

## 📄 License

This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for details.

---

## 🤝 Contributing

Contributions, issues, and suggestions are welcome! Feel free to open a pull request or issue on [GitHub](https://github.com/Mesharsky/Advanced-TeamBalance).

---

## 📞 Support

Support is available via [GitHub issue reports](https://github.com/Mesharsky/Advanced-TeamBalance/issues). Please open an issue for any questions, bug reports, or feature requests.

---

## 🔗 Quick Links

- [Download Latest Release](https://github.com/Mesharsky/Advanced-TeamBalance/releases/latest)
- [Report an Issue](https://github.com/Mesharsky/Advanced-TeamBalance/issues)
- [Contribute on GitHub](https://github.com/Mesharsky/Advanced-TeamBalance)

---

## ✨ Features Coming Soon

- Additional language support.
- More customization options.
- Enhanced performance metrics.

---

## 🤖 Compatibility

- **Counter-Strike Version:** *(Minimum: v282)*

---

## 💰Support / Donations

- **PayPal** You can support me via PayPal, [Donate](https://paypal.me/mesharskyh2k)
