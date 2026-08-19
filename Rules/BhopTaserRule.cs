using CounterStrikeSharp.API.Core;

namespace FunRandomRounds.Rules;

public sealed class BhopTaserRule : RoundRule
{
    private readonly BhopKingRule _bhop;
    private readonly TaserOnlyRule _taser;

    public BhopTaserRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
        _bhop = new BhopKingRule(plugin);
        _taser = new TaserOnlyRule(plugin);
    }

    public override string Name => "身法雷电法王";
    public override string Description => "无限连跳取消限速，仅无限电击枪";
    public override bool BlocksBuy => _taser.BlocksBuy;

    public override void Start()
    {
        _bhop.Start();
        _taser.Start();
    }

    public override void Stop()
    {
        _taser.Stop();
        _bhop.Stop();
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        _taser.OnPlayerSpawn(player);
    }

    public override void OnFreezeEnd()
    {
        _taser.OnFreezeEnd();
    }

    public override void OnTick()
    {
        _bhop.OnTick();
        _taser.OnTick();
    }
}
