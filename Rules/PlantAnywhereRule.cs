using CounterStrikeSharp.API;

namespace FunRandomRounds.Rules;

public sealed class PlantAnywhereRule : RoundRule
{
    private bool _active;

    public PlantAnywhereRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "无限制下包";
    public override string Description => "冻结倒计时结束后，C4可以安装在任何地方";

    public override void Start()
    {
        _active = true;
        Server.ExecuteCommand("mp_plant_c4_anywhere 0");
    }

    public override void OnFreezeEnd()
    {
        if (_active)
            Server.ExecuteCommand("mp_plant_c4_anywhere 1");
    }

    public override void Stop()
    {
        _active = false;
        Server.ExecuteCommand("mp_plant_c4_anywhere 0");
    }
}
