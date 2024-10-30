using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance : BasePlugin
{
    public override string ModuleName => "Mesharsky Team Balance";
    public override string ModuleVersion => "3.1.0";
    public override string ModuleAuthor => "Mesharsky";

    public override void Load(bool hotReload)
    {
        LoadConfiguration();
        Initialize_Events();
        Initialize_Misc();
        AddCommandListener("jointeam", Command_JoinTeam);

        AddTimer(5.0f, () =>
        {
            ConVar.Find("mp_autoteambalance")!.SetValue(false);

            PrintDebugMessage("Convar 'mp_autoteambalance' has been set to 'false'");
        });
    }
}
