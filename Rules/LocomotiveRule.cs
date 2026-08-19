using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace FunRandomRounds.Rules;

public sealed class LocomotiveRule : RoundRule
{
    private const float SpeedMultiplier = 5.0f;

    private readonly RestrictedLoadout _loadout;
    private bool _active;

    public LocomotiveRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
        _loadout = new RestrictedLoadout(
            plugin,
            giveItems: ["weapon_knife"],
            keepParts: [],
            applyCvars: ApplySpawnCvars,
            restoreCvars: RestoreCvars);
    }

    public override string Name => "火车头";
    public override string Description => "只有刀，移动速度 × 5";
    public override bool BlocksBuy => _active;
    public override bool BlocksBuyAndPickup => _active;

    public override bool AllowsWeapon(string designerName)
        => _loadout.AllowsWeapon(designerName);

    public override void Start()
    {
        _active = true;
        _loadout.Start();
        foreach (var player in Plugin.GetCombatPlayers())
            ApplySpeed(player);
    }

    public override void Stop()
    {
        _active = false;
        _loadout.Stop();
        RestoreSpeed();
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!_active)
            return;

        _loadout.OnPlayerSpawn(player);
        ApplySpeed(player);
    }

    public override void OnFreezeEnd()
    {
        if (_active)
            _loadout.OnFreezeEnd();
    }

    public override void OnTick()
    {
        if (!_active)
            return;

        foreach (var player in Plugin.GetCombatPlayers())
            ApplySpeed(player);
    }

    private static void ApplySpeed(CCSPlayerController player)
    {
        if (!WeaponUtil.IsAlivePawn(player, out var pawn))
            return;

        try
        {
            pawn.VelocityModifier = SpeedMultiplier;
            Utilities.SetStateChanged(
                pawn,
                "CCSPlayerPawn",
                "m_flVelocityModifier");
        }
        catch
        {
            // ignored
        }
    }

    private static void RestoreSpeed()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            var pawn = player?.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid)
                continue;

            try
            {
                pawn.VelocityModifier = 1.0f;
                Utilities.SetStateChanged(
                    pawn,
                    "CCSPlayerPawn",
                    "m_flVelocityModifier");
            }
            catch
            {
                // ignored
            }
        }
    }

    private static void ApplySpawnCvars()
    {
        Server.ExecuteCommand("mp_death_drop_gun 0");
        Server.ExecuteCommand("mp_weapons_allow_map_placed 0");
        Server.ExecuteCommand("mp_t_default_primary \"\"");
        Server.ExecuteCommand("mp_ct_default_primary \"\"");
        Server.ExecuteCommand("mp_t_default_secondary \"\"");
        Server.ExecuteCommand("mp_ct_default_secondary \"\"");
        Server.ExecuteCommand("mp_t_default_melee weapon_knife");
        Server.ExecuteCommand("mp_ct_default_melee weapon_knife");
        Server.ExecuteCommand("mp_t_default_grenades \"\"");
        Server.ExecuteCommand("mp_ct_default_grenades \"\"");
    }

    private static void RestoreCvars()
    {
        Server.ExecuteCommand("mp_death_drop_gun 1");
        Server.ExecuteCommand("mp_weapons_allow_map_placed 1");
        Server.ExecuteCommand("mp_t_default_secondary weapon_glock");
        Server.ExecuteCommand("mp_ct_default_secondary weapon_hkp2000");
        Server.ExecuteCommand("mp_t_default_melee weapon_knife");
        Server.ExecuteCommand("mp_ct_default_melee weapon_knife");
    }
}
