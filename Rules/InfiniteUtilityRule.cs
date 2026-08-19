using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace FunRandomRounds.Rules;

public sealed class InfiniteUtilityRule : RoundRule
{
    private static readonly string[] SharedNades =
    [
        "weapon_hegrenade",
        "weapon_flashbang",
        "weapon_smokegrenade",
        "weapon_decoy"
    ];

    private readonly Dictionary<(int Slot, string Part), float> _emptySince = new();
    private bool _active;
    private int _tick;

    public InfiniteUtilityRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "无限道具";
    public override string Description => "额，就是无限道具";

    public override void Start()
    {
        _active = true;
        _tick = 0;
        _emptySince.Clear();
        ApplyCvars();
        foreach (var player in Plugin.GetCombatPlayers())
            GiveKit(player);
    }

    public override void Stop()
    {
        _active = false;
        _emptySince.Clear();
        RestoreCvars();
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!_active)
            return;

        var slot = player.Slot;
        Plugin.AddTimer(0.25f, () =>
        {
            if (!_active)
                return;
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p != null)
                GiveKit(p);
        });
    }

    public override void OnGrenadeThrown(CCSPlayerController player, string weapon)
    {
        if (!_active)
            return;

        var designer = WeaponUtil.UtilityDesignerFromEvent(weapon);
        if (designer == null)
            return;

        _emptySince[(player.Slot, WeaponUtil.NamePartFromDesigner(designer))] = Server.CurrentTime;
    }

    public override void OnTick()
    {
        if (!_active)
            return;

        _tick++;
        if (_tick % 8 != 0)
            return;

        var now = Server.CurrentTime;
        foreach (var player in Plugin.GetCombatPlayers())
        {
            if (!WeaponUtil.IsAlivePawn(player, out var pawn))
            {
                ClearPlayer(player.Slot);
                continue;
            }

            foreach (var item in KitFor(player))
            {
                var part = WeaponUtil.NamePartFromDesigner(item);
                if (HasUtility(pawn, item))
                {
                    _emptySince.Remove((player.Slot, part));
                    continue;
                }

                var key = (player.Slot, part);
                if (!_emptySince.TryGetValue(key, out var since))
                {
                    _emptySince[key] = now;
                    continue;
                }

                if (now - since < 0.45f)
                    continue;

                _emptySince.Remove(key);
                WeaponUtil.GiveIfMissingQuiet(player, item, part);
            }
        }
    }

    private static void GiveKit(CCSPlayerController player)
    {
        foreach (var item in KitFor(player))
            WeaponUtil.GiveIfMissingQuiet(player, item, WeaponUtil.NamePartFromDesigner(item));
    }

    private static IEnumerable<string> KitFor(CCSPlayerController player)
    {
        foreach (var item in SharedNades)
            yield return item;

        yield return player.Team == CsTeam.CounterTerrorist
            ? "weapon_incgrenade"
            : "weapon_molotov";
    }

    private static bool HasUtility(CCSPlayerPawn pawn, string item)
    {
        var part = WeaponUtil.NamePartFromDesigner(item);
        if (part is "molotov" or "incgrenade")
            return WeaponUtil.CountWeapon(pawn, "molotov") > 0 ||
                   WeaponUtil.CountWeapon(pawn, "incgrenade") > 0;

        return WeaponUtil.CountWeapon(pawn, part) > 0;
    }

    private void ClearPlayer(int slot)
    {
        var keys = _emptySince.Keys.Where(key => key.Slot == slot).ToList();
        foreach (var key in keys)
            _emptySince.Remove(key);
    }

    private static void ApplyCvars()
    {
        Server.ExecuteCommand("ammo_grenade_limit_default 5");
        Server.ExecuteCommand("ammo_grenade_limit_flashbang 5");
        Server.ExecuteCommand("ammo_grenade_limit_total 20");
        Server.ExecuteCommand("mp_buy_allow_grenades 1");
    }

    private static void RestoreCvars()
    {
        Server.ExecuteCommand("ammo_grenade_limit_default 1");
        Server.ExecuteCommand("ammo_grenade_limit_flashbang 2");
        Server.ExecuteCommand("ammo_grenade_limit_total 4");
    }
}
