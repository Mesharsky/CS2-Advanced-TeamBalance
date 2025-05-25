<<<<<<< HEAD
<<<<<<< HEAD
=======
>>>>>>> 182a34f (Merge local)
# Advanced Team Balance

<div align="center">

[![Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/mesharsky)

**Support the development of this project**

</div>

A powerful and configurable team balancing plugin for CS2 servers that ensures fair and balanced gameplay.

## Features

- **🎯 Multiple Balance Methods**: Choose between KD, KDA, Score, or Win Rate balancing
- **🔄 Dynamic Balance Events**: Balance teams on round start, player join/leave, or freeze time end
- **🔬 Smart Algorithms**: Intelligently moves and swaps players to create balanced teams
- **🚀 Losing Team Boost**: Helps teams recover from losing streaks by assigning better players
- **👥 Team Size Control**: Maintains team sizes within configurable limits
- **🔐 Admin Exemption**: Server admins can be exempted from automatic team switching
- **🌎 Multiple Languages**: Supports translations system
- **🎮 Safe Switching**: Never switches alive players - waits until they die

## Installation

1. Make sure [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) is installed
2. Download the latest release
3. Drop&Down for installation, if ur dumb, unlucky lil bro.
4. Edit the config file in `addons/counterstrikesharp/configs/plugins/Mesharsky_AdvancedTeamBalance/Mesharsky_AdvancedTeamBalance.toml`

## How It Works

### Team Size Balancing
If one team has more players than allowed by `MaxTeamSizeDifference`, the plugin will move players from the larger team to the smaller team, selecting players with lower skill values first.

### Skill Balancing
The plugin calculates team strength based on your chosen `BalanceMode` (KD, KDA, Score, or Win Rate), then swaps players between teams to balance skill levels while maintaining team sizes.

### Losing Team Boosting
When a team loses multiple rounds in a row (configured by `BoostAfterLoseStreak`), the plugin will prioritize giving better players to the losing team during balancing to help them recover.

### Auto-Scramble
After one team wins multiple rounds in a row (configured by `AutoScrambleAfterWinStreak`), the plugin can scramble all teams either randomly or based on skill.

## Translations

The plugin supports multiple languages:
- English (en)
- Russian (ru)
- Polish (pl)
- German (de)
- French (fr)
- Spanish (es)
- Portuguese (pt)
- Turkish (tr)

To add a new language or modify existing translations, edit the files in the `lang` folder.

## Admin Commands & Permissions

Admins with the `@css/ban` flag (configurable) are exempted from automatic team switching.

## Credits

- **Author**: Mesharsky
- **Version**: 5.0.0

## Need Help?

If you encounter any issues or have questions, please open an issue on the GitHub repository.

Happy balancing! 🎮

## Wanna donate?

Well i hope you do. https://paypal.me/mesharskyh2k
<<<<<<< HEAD
=======
# Advanced Team Balance

A powerful and configurable team balancing plugin for CS2 servers that ensures fair and balanced gameplay.

## Features

- **🎯 Multiple Balance Methods**: Choose between KD, KDA, Score, or Win Rate balancing
- **🔄 Dynamic Balance Events**: Balance teams on round start, player join/leave, or freeze time end
- **🔬 Smart Algorithms**: Intelligently moves and swaps players to create balanced teams
- **🚀 Losing Team Boost**: Helps teams recover from losing streaks by assigning better players
- **👥 Team Size Control**: Maintains team sizes within configurable limits
- **🔐 Admin Exemption**: Server admins can be exempted from automatic team switching
- **🌎 Multiple Languages**: Supports translations system
- **🎮 Safe Switching**: Never switches alive players - waits until they die

## Installation

1. Make sure [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) is installed
2. Download the latest release
3. Drop&Down for installation, if ur dumb, unlucky lil bro.
4. Edit the config file in `addons/counterstrikesharp/configs/plugins/Mesharsky_AdvancedTeamBalance/Mesharsky_AdvancedTeamBalance.toml`

## How It Works

### Team Size Balancing
If one team has more players than allowed by `MaxTeamSizeDifference`, the plugin will move players from the larger team to the smaller team, selecting players with lower skill values first.

### Skill Balancing
The plugin calculates team strength based on your chosen `BalanceMode` (KD, KDA, Score, or Win Rate), then swaps players between teams to balance skill levels while maintaining team sizes.

### Losing Team Boosting
When a team loses multiple rounds in a row (configured by `BoostAfterLoseStreak`), the plugin will prioritize giving better players to the losing team during balancing to help them recover.

### Auto-Scramble
After one team wins multiple rounds in a row (configured by `AutoScrambleAfterWinStreak`), the plugin can scramble all teams either randomly or based on skill.

## Translations

The plugin supports multiple languages:
- English (en)
- Russian (ru)
- Polish (pl)
- German (de)
- French (fr)
- Spanish (es)
- Portuguese (pt)
- Turkish (tr)

To add a new language or modify existing translations, edit the files in the `lang` folder.

## Admin Commands & Permissions

Admins with the `@css/ban` flag (configurable) are exempted from automatic team switching.

## Credits

- **Author**: Mesharsky
- **Version**: 5.0.0

## Need Help?

If you encounter any issues or have questions, please open an issue on the GitHub repository.

Happy balancing! 🎮

## Wanna donate?

Well i hope you do. https://paypal.me/mesharskyh2k
>>>>>>> 74fc461956bce7b38451c220f391a210b6af4d41
=======
>>>>>>> 182a34f (Merge local)
