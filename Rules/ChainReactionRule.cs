using CounterStrikeSharp.API;

namespace FunRandomRounds.Rules;

public sealed class ChainReactionRule : RoundRule
{
    public ChainReactionRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "连锁反应";
    public override string Description => "地上的道具可以收到伤害并引爆";

    public override void Start()
    {
        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("mp_shoot_dropped_grenades 1");
        Server.ExecuteCommand("sv_cheats 0");
    }

    public override void Stop()
    {
        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("mp_shoot_dropped_grenades 0");
        Server.ExecuteCommand("sv_cheats 0");
    }
}
