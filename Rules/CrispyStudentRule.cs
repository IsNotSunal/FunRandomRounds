using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace FunRandomRounds.Rules;

public sealed class CrispyStudentRule : RoundRule
{
    private readonly RestrictedLoadout _loadout;
    private readonly Dictionary<int, float> _emptySince = new();
    private bool _active;
    private int _tick;

    public CrispyStudentRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
        _loadout = new RestrictedLoadout(
            plugin,
            giveItems: ["weapon_decoy"],
            keepParts: ["decoy"],
            applyCvars: ApplySpawnCvars,
            restoreCvars: RestoreCvars);
    }

    public override string Name => "脆皮大学生";
    public override string Description => "所有人1血，开局诱饵弹，不能购买";
    public override bool BlocksBuy => _active;

    public override void Start()
    {
        _active = true;
        _tick = 0;
        _emptySince.Clear();
        foreach (var player in Plugin.GetCombatPlayers())
            ClampFragile(player);
        _loadout.Start();
    }

    public override void Stop()
    {
        _active = false;
        _emptySince.Clear();
        _loadout.Stop();
        foreach (var player in Plugin.GetCombatPlayers())
            RestoreHealth(player);
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!_active)
            return;

        ClampFragile(player);
        _loadout.OnPlayerSpawn(player);
    }

    public override void OnFreezeEnd()
    {
        if (_active)
            _loadout.OnFreezeEnd();
    }

    public override void OnGrenadeThrown(CCSPlayerController player, string weapon)
    {
        if (!_active || !weapon.Contains("decoy", StringComparison.OrdinalIgnoreCase))
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
            ClampFragile(player);
            if (!WeaponUtil.IsAlivePawn(player, out var pawn))
            {
                _emptySince.Remove(player.Slot);
                continue;
            }

            if (WeaponUtil.CountWeapon(pawn, "decoy") > 0)
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
            WeaponUtil.GiveRestricted(Plugin, player, "weapon_decoy", "decoy", "decoy");
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
        Server.ExecuteCommand("mp_t_default_grenades weapon_decoy");
        Server.ExecuteCommand("mp_ct_default_grenades weapon_decoy");
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

    private static void ClampFragile(CCSPlayerController player)
    {
        if (!WeaponUtil.IsAlivePawn(player, out var pawn))
            return;

        if (pawn.MaxHealth != 1)
        {
            pawn.MaxHealth = 1;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iMaxHealth");
        }

        if (pawn.Health != 1)
        {
            pawn.Health = 1;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        }

        if (pawn.ArmorValue != 0)
        {
            pawn.ArmorValue = 0;
            Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_ArmorValue");
        }
    }

    private static void RestoreHealth(CCSPlayerController player)
    {
        if (!WeaponUtil.IsAlivePawn(player, out var pawn))
            return;

        pawn.MaxHealth = 100;
        if (pawn.Health > 100)
            pawn.Health = 100;
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iMaxHealth");
    }
}
