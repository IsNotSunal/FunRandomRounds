using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Cvars;

namespace FunRandomRounds;

internal sealed class CvarSnapshot
{
    private static readonly string[] Names =
    [
        "sv_cheats",
        "sv_gravity",
        "sv_airaccelerate",
        "sv_maxvelocity",
        "sv_enablebunnyhopping",
        "sv_autobunnyhopping",
        "sv_staminamax",
        "sv_staminalandcost",
        "sv_staminajumpcost",
        "sv_staminarecoveryrate",
        "sv_accelerate_use_weapon_speed",
        "weapon_accuracy_nospread",
        "weapon_air_spread_scale",
        "weapon_accuracy_reset_on_deploy",
        "sv_strafing_inaccuracy_enabled",
        "sv_turning_inaccuracy_enabled",
        "sv_infinite_ammo",
        "mp_buytime",
        "sv_buy_status_override",
        "mp_buy_anywhere",
        "mp_buy_during_immunity",
        "mp_buy_allow_guns",
        "mp_buy_allow_grenades",
        "mp_death_drop_gun",
        "mp_death_drop_grenade",
        "mp_drop_knife_enable",
        "mp_weapons_allow_map_placed",
        "mp_taser_recharge_time",
        "mp_plant_c4_anywhere",
        "mp_shoot_dropped_grenades",
        "ammo_grenade_limit_default",
        "ammo_grenade_limit_flashbang",
        "ammo_grenade_limit_total",
        "mp_friendlyfire",
        "ff_damage_reduction_bullets",
        "ff_damage_reduction_grenade",
        "ff_damage_reduction_other",
        "mp_autokick",
        "mp_tkpunish",
        "sv_disable_radar",
        "mp_radar_showall",
        "mp_t_default_primary",
        "mp_t_default_secondary",
        "mp_t_default_melee",
        "mp_t_default_grenades",
        "mp_ct_default_primary",
        "mp_ct_default_secondary",
        "mp_ct_default_melee",
        "mp_ct_default_grenades"
    ];

    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public void Capture()
    {
        _values.Clear();
        foreach (var name in Names)
        {
            try
            {
                var cvar = ConVar.Find(name);
                if (cvar != null)
                    _values[name] = cvar.StringValue ?? string.Empty;
            }
            catch
            {
                // ignored
            }
        }
    }

    public void Restore()
    {
        if (_values.Count == 0)
            return;

        Server.ExecuteCommand("sv_cheats 1");
        foreach (var (name, value) in _values)
        {
            if (name.Equals("sv_cheats", StringComparison.OrdinalIgnoreCase))
                continue;
            Server.ExecuteCommand($"{name} {Quote(value)}");
        }

        if (_values.TryGetValue("sv_cheats", out var cheats))
            Server.ExecuteCommand($"sv_cheats {Quote(cheats)}");

        _values.Clear();
    }

    private static string Quote(string value)
    {
        if (value.Length == 0 || value.Contains(' ') || value.Contains('"'))
            return $"\"{value.Replace("\"", "\\\"")}\"";
        return value;
    }
}
