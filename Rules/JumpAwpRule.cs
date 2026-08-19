using CounterStrikeSharp.API;

namespace FunRandomRounds.Rules;

public sealed class JumpAwpRule : RoundRule
{
    public JumpAwpRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "跳狙飞人";
    public override string Description => "重力减小，枪械无扩散";

    public override void Start()
    {
        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("sv_gravity 280");
        Server.ExecuteCommand("sv_airaccelerate 80");
        Server.ExecuteCommand("weapon_accuracy_nospread 1");
        Server.ExecuteCommand("sv_cheats 0");
    }

    public override void Stop()
    {
        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("sv_gravity 800");
        Server.ExecuteCommand("sv_airaccelerate 12");
        Server.ExecuteCommand("weapon_accuracy_nospread 0");
        Server.ExecuteCommand("sv_cheats 0");
    }
}
