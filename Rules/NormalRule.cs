namespace FunRandomRounds.Rules;

public sealed class NormalRule : RoundRule
{
    public NormalRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "正常";
    public override string Description => "正常比赛";
}
