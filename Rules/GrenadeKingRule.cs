using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace FunRandomRounds.Rules;

public sealed class GrenadeKingRule : RoundRule
{
    private readonly RestrictedLoadout _loadout;
    private readonly Dictionary<int, float> _emptySince = new();
    private bool _active;
    private int _tick;

    public GrenadeKingRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
        _loadout = new RestrictedLoadout(
            plugin,
            giveItems: ["weapon_hegrenade"],
            keepParts: ["hegrenade"],
            applyCvars: ApplySpawnCvars,
            restoreCvars: RestoreCvars);
    }

    public override string Name => "玉面手雷王";
    public override string Description => "开局无限手雷，不能购买";
    public override bool BlocksBuy => _active;

    public override void Start()
    {
        _active = true;
        _tick = 0;
        _emptySince.Clear();
        _loadout.Start();
    }

    public override void Stop()
    {
        _active = false;
        _emptySince.Clear();
        _loadout.Stop();
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (_active)
            _loadout.OnPlayerSpawn(player);
    }

    public override void OnFreezeEnd()
    {
        if (_active)
            _loadout.OnFreezeEnd();
    }

    public override void OnGrenadeThrown(CCSPlayerController player, string weapon)
    {
        if (!_active || !IsHeWeapon(weapon))
            return;

        _emptySince[player.Slot] = Server.CurrentTime;
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
                _emptySince.Remove(player.Slot);
                continue;
            }

            if (WeaponUtil.CountWeapon(pawn, "hegrenade") > 0)
            {
                _emptySince.Remove(player.Slot);
                continue;
            }

            if (!_emptySince.TryGetValue(player.Slot, out var since))
            {
                _emptySince[player.Slot] = now;
                continue;
            }

            if (now - since < 0.45f)
                continue;

            _emptySince.Remove(player.Slot);
            WeaponUtil.GiveRestricted(Plugin, player, "weapon_hegrenade", "hegrenade", "hegrenade");
        }
    }

    private static void ApplySpawnCvars()
    {
        Server.ExecuteCommand("ammo_grenade_limit_default 5");
        Server.ExecuteCommand("ammo_grenade_limit_total 5");
        Server.ExecuteCommand("mp_death_drop_gun 1");
        Server.ExecuteCommand("mp_weapons_allow_map_placed 1");
        Server.ExecuteCommand("mp_t_default_primary \"\"");
        Server.ExecuteCommand("mp_ct_default_primary \"\"");
        Server.ExecuteCommand("mp_t_default_secondary \"\"");
        Server.ExecuteCommand("mp_ct_default_secondary \"\"");
        Server.ExecuteCommand("mp_t_default_melee weapon_knife");
        Server.ExecuteCommand("mp_ct_default_melee weapon_knife");
        Server.ExecuteCommand("mp_t_default_grenades weapon_hegrenade");
        Server.ExecuteCommand("mp_ct_default_grenades weapon_hegrenade");
    }

    private static void RestoreCvars()
    {
        Server.ExecuteCommand("mp_buytime 20");
        Server.ExecuteCommand("mp_death_drop_gun 1");
        Server.ExecuteCommand("mp_weapons_allow_map_placed 1");
        Server.ExecuteCommand("ammo_grenade_limit_default 1");
        Server.ExecuteCommand("ammo_grenade_limit_total 4");
        Server.ExecuteCommand("mp_t_default_secondary weapon_glock");
        Server.ExecuteCommand("mp_ct_default_secondary weapon_hkp2000");
        Server.ExecuteCommand("mp_t_default_melee weapon_knife");
        Server.ExecuteCommand("mp_ct_default_melee weapon_knife");
        Server.ExecuteCommand("mp_t_default_grenades \"\"");
        Server.ExecuteCommand("mp_ct_default_grenades \"\"");
    }

    private static bool IsHeWeapon(string weapon)
    {
        return !string.IsNullOrWhiteSpace(weapon) &&
               (weapon.Contains("hegrenade", StringComparison.OrdinalIgnoreCase) ||
                weapon.Equals("he", StringComparison.OrdinalIgnoreCase));
    }
}
