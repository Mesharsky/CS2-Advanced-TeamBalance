using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

namespace Mesharsky_TeamBalance;

public partial class Mesharsky_TeamBalance : BasePlugin
{
    public override string ModuleName => "Mesharsky Team Balance";
    public override string ModuleVersion => "0.5";
    public override string ModuleAuthor => "Mesharsky";

    public override void Load(bool hotReload)
    {
        LoadConfiguration();
        Initialize_Events();
        AddCommandListener("jointeam", Command_JoinTeam);

        AddTimer(7.0f, () =>
        {
            string conVarName = "mp_autoteambalance";
            ConVar? cvar = ConVar.Find(conVarName);

            if (cvar == null)
                return;

            cvar.SetValue(false);

            PrintDebugMessage("Convar 'mp_autoteambalance' has been set to 'false'");
        });
    }
}
