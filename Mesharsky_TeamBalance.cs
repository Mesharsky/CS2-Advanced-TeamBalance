using CounterStrikeSharp.API.Core;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance : BasePlugin
{
    public override string ModuleName => "Mesharsky Team Balance";
    public override string ModuleVersion => "0.1";
    public override string ModuleAuthor => "Mesharsky";

    private bool BalanceHasBeenMade = false;

    public override void Load(bool hotReload)
    {
        LoadConfiguration();
        Initialize_Events();

        AddCommandListener("jointeam", Command_JoinTeam);
    }
}
