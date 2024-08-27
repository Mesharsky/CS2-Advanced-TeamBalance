# Mesharsky Team Balance Plugin + Scramble Mode Support

This plugin is designed to ensure fair and balanced gameplay by intelligently managing team sizes and performance metrics. It's highly configurable to suit the needs of any server.

## Features

- **Intelligent Team Balancing**: Automatically balances teams based on player performance metrics (Kills, Deaths, Damage, and Score).
- **Configurable Settings**: Fine-tune the balancing behavior with various configuration options.
- **Performance-Based**: Optionally use a custom `PerformanceScore` to evaluate players and create fairer teams.
- **Team Size Control**: Ensures the difference in team sizes is kept to a minimum.
- **Scramble Mode**: Supports scramble modes, with multiple configuration options.

## Installation

1. **Download the Plugin**: Download from releases tab directly: [Advanced-TeamBalance Releases](https://github.com/Mesharsky/Advanced-TeamBalance/releases)
2. **Upload plugin to your CounterStrikeSharp folder**: Place the plugin folder inside the CounterStrikeSharp folder. Standard installation.
3. **Configure the Plugin**: Edit the `TeamBalance.toml` file to adjust the settings according to your server's needs (see below for details).
4. **Restart Your Server**: Restart the server to load the plugin with your customized settings.

## Configuration

The plugin comes with a configuration file, `TeamBalance.toml`, that allows you to customize its behavior. Below is a detailed explanation of each setting. (Configuration file is inside the Module Directory)

```toml
# Plugin Author - Mesharsky
# https://csowicze.pl/

# Team Balance Plugin Configuration
# Make sure to adjust these settings according to your server's needs.

[PluginSettings]
# The minimum number of players required on the server before the team balance
# feature activates. This prevents balancing when there are too few players.
# Default: 4
minimum_players = 4

# The maximum allowed ratio of scores between teams before triggering a balance.
# For example, if set to 1.6, the balance will trigger if one team's score is
# 60% higher than the other team's score. Adjust this value based on how strict
# you want the balancing to be.
# Default: 2.0
score_balance_ratio = 2.0

# Whether to use PerformanceScore for balancing.
# PerformanceScore is a custom metric that considers KDA (Kills, Deaths, Assists),
# damage dealt, and the in-game score to evaluate a player's overall performance.
# If set to true, the balance algorithm will use PerformanceScore to evaluate 
# players when balancing teams, rather than just the in-game score.
# Default: true
use_performance_score = true

# Maximum allowed difference in team sizes.
# This setting controls how much the team sizes are allowed to differ after balancing.
# If set to 1, the algorithm will attempt to ensure that the difference in the number 
# of players between the teams is no more than one. This helps prevent one team from
# having a significant numerical advantage over the other.
# Default: 1
max_team_size_difference = 1

# Enable or disable debug messages.
# If set to true, the plugin will print debug messages to the console.
# Default: true
enable_debug_messages = true

# Enable or disable chat messages.
# If set to true, the plugin will print messages to the chat.
# Default: true
enable_chat_messages = true

# Scramble Mode Configuration
# scramble_mode determines the type of scrambling behavior.
# Options: 
# 
# "none" (no scrambling)
# "round" (scramble teams every X rounds),
# "winstreak" (scramble if a team wins X rounds in a row)
# "halftime" (scramble at halftime).
#
# Default: "none"
scramble_mode = "none"

# Number of rounds after which teams should be scrambled (used if scramble_mode is "round").
# Default: 5
round_scramble_interval = 5

# Number of consecutive wins required to trigger a scramble (used if scramble_mode is "winstreak").
# Default: 3
winstreak_scramble_threshold = 3

# Enable or disable halftime scrambling.
# If set to true and scramble_mode is "halftime", teams will be scrambled at halftime.
# Default: false
halftime_scramble_enabled = false
```
## Key Settings Explained

- **`minimum_players`**: The minimum number of players required before the plugin activates. This ensures that balancing doesn't occur when there are too few players to make meaningful adjustments.

- **`score_balance_ratio`**: Controls the ratio of scores between teams that will trigger a rebalance. For example, a ratio of 1.6 means that if one team’s score is 60% higher than the other’s, a balance will be triggered.

- **`use_performance_score`**: When enabled, the plugin uses a custom `PerformanceScore` metric (based on KDA, damage, and score) to determine player value during balancing. This typically results in more effective balancing than using the in-game score alone.

- **`max_team_size_difference`**: Ensures that the team sizes differ by no more than this value after balancing, helping to prevent one team from having a significant player advantage.

- **`scramble_mode`**: Allows to automatically scramble teams based on the conditions set.

## How It Works

1. **Player Stats Collection**: At the start of each round, the plugin collects stats for each player (Kills, Deaths, Damage, Score) and stores them in a cache.

2. **Balance Check**: The plugin checks whether teams need to be rebalanced based on player count and score ratios.

3. **Team Balancing**:
   - If balancing is required, players are evaluated based on their `PerformanceScore` (or just team size if `use_performance_score` is disabled).
   - The plugin attempts to distribute players between teams to minimize the score difference while respecting the `max_team_size_difference`.
   - Only players who need to be moved are affected; those already on the correct team are left in place.

4. **Feedback**: If a balance is made, players are notified via in-game chat, ensuring transparency.

## Example Scenarios

### Scenario 1: Uneven Team Sizes
- **Input**: 10 players on the Terrorist team, 1 player on the Counter-Terrorist team.
- **Result**: The plugin will move players from the Terrorist team to the Counter-Terrorist team until the difference in team sizes is within the allowed range (defined by `max_team_size_difference`).

### Scenario 2: High Score Disparity
- **Input**: Terrorist team has a score that is 70% higher than the Counter-Terrorist team.
- **Result**: The plugin will trigger a rebalance, moving top-performing players from the Terrorist team to the Counter-Terrorist team to even out the scores.

## Development

This plugin was developed by Mesharsky. Contributions, issues, and suggestions are welcome! Feel free to open a pull request or issue on GitHub.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
