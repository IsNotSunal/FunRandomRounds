using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace FunRandomRounds.Rules;

public sealed class RandomWeaponOnKillRule : RoundRule
{
    private static readonly string[] Rifles =
    [
        "weapon_famas",
        "weapon_galilar",
        "weapon_m4a1",
        "weapon_m4a1_silencer",
        "weapon_ak47",
        "weapon_aug",
        "weapon_sg556"
    ];

    private static readonly string[] Pistols =
    [
        "weapon_glock",
        "weapon_hkp2000",
        "weapon_usp_silencer",
        "weapon_p250",
        "weapon_fiveseven",
        "weapon_tec9",
        "weapon_cz75a",
        "weapon_elite",
        "weapon_deagle",
        "weapon_revolver"
    ];

    private readonly Dictionary<int, int> _generation = new();
    private readonly Random _random = new();
    private bool _active;

    public RandomWeaponOnKillRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "随机武器";
    public override string Description => "击杀时步枪换随机步枪，手枪换随机手枪";

    public override void Start()
    {
        _active = true;
        _generation.Clear();
    }

    public override void Stop()
    {
        _active = false;
        _generation.Clear();
    }

    public override void OnPlayerDeath(EventPlayerDeath @event)
    {
        if (!_active)
            return;

        var victim = @event.Userid;
        var attacker = @event.Attacker;
        if (attacker == null || !attacker.IsValid ||
            victim == null || !victim.IsValid ||
            attacker.SteamID == victim.SteamID ||
            attacker.Team == victim.Team ||
            !WeaponUtil.IsAlivePawn(attacker, out var pawn))
            return;

        var previous = pawn.WeaponServices?.ActiveWeapon.Value?.DesignerName
                       ?? string.Empty;
        var pool = GetPool(previous);
        if (pool == null)
            return;

        var selected = PickDifferent(previous, pool);
        var slot = attacker.Slot;
        var generation = _generation.TryGetValue(slot, out var current)
            ? current + 1
            : 1;
        _generation[slot] = generation;

        attacker.RemoveItemByDesignerName(previous);
        Plugin.AddTimer(0.15f, () =>
            GiveRandomWeapon(slot, generation, selected));
    }

    private void GiveRandomWeapon(
        int slot,
        int generation,
        string weapon)
    {
        if (!_active ||
            !_generation.TryGetValue(slot, out var latest) ||
            latest != generation)
            return;

        var player = Utilities.GetPlayerFromSlot(slot);
        if (player == null || !WeaponUtil.IsAlivePawn(player, out _))
            return;

        WeaponUtil.Give(player, weapon);
        WeaponUtil.Equip(
            player,
            WeaponUtil.NamePartFromDesigner(weapon));
    }

    private static string[]? GetPool(string weapon)
    {
        if (Rifles.Contains(weapon, StringComparer.OrdinalIgnoreCase))
            return Rifles;
        if (Pistols.Contains(weapon, StringComparer.OrdinalIgnoreCase))
            return Pistols;
        return null;
    }

    private string PickDifferent(string previous, string[] pool)
    {
        if (pool.Length == 1)
            return pool[0];

        string selected;
        do
        {
            selected = pool[_random.Next(pool.Length)];
        } while (selected.Equals(
                     previous,
                     StringComparison.OrdinalIgnoreCase));

        return selected;
    }
}
