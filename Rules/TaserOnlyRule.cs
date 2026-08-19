using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace FunRandomRounds.Rules;

public sealed class TaserOnlyRule : RoundRule
{
    private bool _active;
    private int _tick;
    private int _epoch;

    public TaserOnlyRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "雷电法王";
    public override string Description => "开局电击枪且无限充能，不能购买";
    public override bool BlocksBuy => _active;

    public override void Start()
    {
        _active = true;
        _tick = 0;
        _epoch++;
        LockBuy();
        ApplySpawnCvars();
        foreach (var player in Plugin.GetCombatPlayers())
            StripThenGive(player);

        var epoch = _epoch;
        Plugin.AddTimer(1.0f, () =>
        {
            if (_active && _epoch == epoch)
                LockBuy();
        });
    }

    public override void Stop()
    {
        _active = false;
        _epoch++;
        RestoreCvars();
        RestrictedLoadout.UnlockBuy();
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!_active)
            return;

        LockBuy();
        var slot = player.Slot;
        Plugin.AddTimer(0.20f, () =>
        {
            if (!_active)
                return;
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p != null)
                StripThenGive(p);
        });
    }

    public override void OnFreezeEnd()
    {
        if (!_active)
            return;

        LockBuy();
    }

    public override void OnTick()
    {
        if (!_active)
            return;

        _tick++;
        if (_tick % 8 != 0)
            return;

        foreach (var player in Plugin.GetCombatPlayers())
        {
            if (!WeaponUtil.IsAlivePawn(player, out var pawn))
                continue;

            WeaponUtil.GiveIfMissingQuiet(player, "weapon_taser", "taser");
            RefillTaser(pawn);
        }
    }

    private void StripThenGive(CCSPlayerController player)
    {
        Server.ExecuteCommand("mp_drop_knife_enable 1");
        WeaponUtil.DropAllKeepC4(player);

        var slot = player.Slot;
        Plugin.AddTimer(0.15f, () =>
        {
            if (!_active)
                return;

            var p = Utilities.GetPlayerFromSlot(slot);
            if (p == null)
                return;

            WeaponUtil.GiveIfMissingQuiet(p, "weapon_taser", "taser");
            WeaponUtil.EquipTaser(p);
        });
    }

    private static void LockBuy()
    {
        Server.ExecuteCommand("sv_buy_status_override 3");
        Server.ExecuteCommand("mp_buytime 0");
        Server.ExecuteCommand("mp_buy_anywhere 0");
        Server.ExecuteCommand("mp_buy_during_immunity 0");
        Server.ExecuteCommand("mp_buy_allow_grenades 0");
    }

    private static void ApplySpawnCvars()
    {
        Server.ExecuteCommand("mp_taser_recharge_time 0");
        Server.ExecuteCommand("mp_death_drop_gun 1");
        Server.ExecuteCommand("mp_weapons_allow_map_placed 1");
        Server.ExecuteCommand("mp_t_default_primary \"\"");
        Server.ExecuteCommand("mp_ct_default_primary \"\"");
        Server.ExecuteCommand("mp_t_default_secondary \"\"");
        Server.ExecuteCommand("mp_ct_default_secondary \"\"");
        Server.ExecuteCommand("mp_t_default_melee \"\"");
        Server.ExecuteCommand("mp_ct_default_melee \"\"");
        Server.ExecuteCommand("mp_drop_knife_enable 1");
    }

    private static void RestoreCvars()
    {
        Server.ExecuteCommand("mp_buytime 20");
        Server.ExecuteCommand("mp_death_drop_gun 1");
        Server.ExecuteCommand("mp_weapons_allow_map_placed 1");
        Server.ExecuteCommand("mp_taser_recharge_time 30");
        Server.ExecuteCommand("mp_t_default_secondary weapon_glock");
        Server.ExecuteCommand("mp_ct_default_secondary weapon_hkp2000");
        Server.ExecuteCommand("mp_t_default_melee weapon_knife");
        Server.ExecuteCommand("mp_ct_default_melee weapon_knife");
    }

    private static void RefillTaser(CCSPlayerPawn pawn)
    {
        var weapons = pawn.WeaponServices?.MyWeapons;
        if (weapons == null)
            return;

        foreach (var handle in weapons)
        {
            var weapon = handle.Value;
            if (weapon == null || !weapon.IsValid)
                continue;

            if (!(weapon.DesignerName ?? string.Empty).Contains("taser", StringComparison.OrdinalIgnoreCase) &&
                !(weapon.DesignerName ?? string.Empty).Contains("zeus", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (weapon.AttributeManager?.Item?.ItemDefinitionIndex != 31)
                        continue;
                }
                catch
                {
                    continue;
                }
            }

            try
            {
                if (weapon.Clip1 != 1)
                    weapon.Clip1 = 1;
            }
            catch
            {
                // ignored
            }
        }
    }
}
