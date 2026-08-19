namespace FunRandomRounds.Rules;

public static class RuleRegistry
{
    // 以后加规则：新建 RoundRule 子类，然后在这里登记一行。
    public static IReadOnlyList<Func<FunRandomRoundsPlugin, RoundRule>> Factories { get; } =
    [
        plugin => new NormalRule(plugin),
        plugin => new GrenadeKingRule(plugin),
        plugin => new KillTeleportRule(plugin),
        plugin => new JumpAwpRule(plugin),
        plugin => new VampireRule(plugin),
        plugin => new WallhackRule(plugin),
        plugin => new BhopKingRule(plugin),
        plugin => new TaserOnlyRule(plugin),
        plugin => new CrispyStudentRule(plugin),
        plugin => new PlantAnywhereRule(plugin),
        plugin => new MissDropGunRule(plugin),
        plugin => new InfiniteUtilityRule(plugin),
        plugin => new SacrificeTeammateRule(plugin),
        plugin => new HideRule(plugin),
        plugin => new ChainReactionRule(plugin),
        plugin => new CrossFireRule(plugin),
        plugin => new SmallPlayerRule(plugin),
        plugin => new BhopTaserRule(plugin),
        plugin => new ChainReactionKingRule(plugin),
        plugin => new ForcedSpreadRule(plugin),
        plugin => new LocomotiveRule(plugin),
        plugin => new SpawnShuffleRule(plugin),
        plugin => new TeamShuffleRule(plugin),
        plugin => new PossessionRule(plugin),
        plugin => new RandomWeaponOnKillRule(plugin)
    ];

    public static List<RoundRule> CreateAll(FunRandomRoundsPlugin plugin)
    {
        return Factories.Select(factory => factory(plugin)).ToList();
    }
}
