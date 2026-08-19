using CounterStrikeSharp.API.Core;

namespace FunRandomRounds.Rules;

public sealed class ChainReactionKingRule : RoundRule
{
    private readonly InfiniteUtilityRule _infiniteUtility;
    private readonly ChainReactionRule _chainReaction;

    public ChainReactionKingRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
        _infiniteUtility = new InfiniteUtilityRule(plugin);
        _chainReaction = new ChainReactionRule(plugin);
    }

    public override string Name => "连锁反应大王";
    public override string Description => "无限道具，地上道具可受到伤害并引爆";

    public override void Start()
    {
        _infiniteUtility.Start();
        _chainReaction.Start();
    }

    public override void Stop()
    {
        _infiniteUtility.Stop();
        _chainReaction.Stop();
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        _infiniteUtility.OnPlayerSpawn(player);
    }

    public override void OnGrenadeThrown(CCSPlayerController player, string weapon)
    {
        _infiniteUtility.OnGrenadeThrown(player, weapon);
    }

    public override void OnTick()
    {
        _infiniteUtility.OnTick();
    }
}
