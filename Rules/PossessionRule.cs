using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace FunRandomRounds.Rules;

public sealed class PossessionRule : RoundRule
{
    private readonly Dictionary<int, int> _generation = new();
    private readonly Dictionary<int, Loadout> _snapshots = new();
    private bool _active;

    public PossessionRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "夺舍的来";
    public override string Description => "击杀人继承对方武器和道具";

    public override void Start()
    {
        _active = true;
        _generation.Clear();
        _snapshots.Clear();
        Server.ExecuteCommand("mp_death_drop_gun 0");
        Server.ExecuteCommand("mp_death_drop_grenade 0");
        foreach (var player in Plugin.GetCombatPlayers())
        {
            if (WeaponUtil.IsAlivePawn(player, out var pawn))
                _snapshots[player.Slot] = Capture(player, pawn);
        }
    }

    public override void Stop()
    {
        _active = false;
        _generation.Clear();
        _snapshots.Clear();
        Server.ExecuteCommand("mp_death_drop_gun 1");
        Server.ExecuteCommand("mp_death_drop_grenade 2");
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        _generation.Remove(player.Slot);
        _snapshots.Remove(player.Slot);
    }

    public override void OnTick()
    {
        if (!_active)
            return;

        foreach (var player in Plugin.GetCombatPlayers())
        {
            if (_generation.ContainsKey(player.Slot))
                continue;
            if (WeaponUtil.IsAlivePawn(player, out var pawn))
                _snapshots[player.Slot] = Capture(player, pawn);
        }
    }

    public override void OnPlayerDeath(EventPlayerDeath @event)
    {
        if (!_active)
            return;

        var victim = @event.Userid;
        var attacker = @event.Attacker;
        if (!IsEnemyKill(attacker, victim) ||
            !WeaponUtil.IsAlivePawn(attacker, out _))
            return;

        var victimPawn = victim!.PlayerPawn.Value;
        if (victimPawn == null || !victimPawn.IsValid)
            return;

        if (!_snapshots.TryGetValue(victim.Slot, out var loadout))
            loadout = Capture(victim, victimPawn);
        var slot = attacker!.Slot;
        var generation = _generation.TryGetValue(slot, out var current)
            ? current + 1
            : 1;
        _generation[slot] = generation;

        WeaponUtil.DropAllKeepC4(attacker);
        Plugin.AddTimer(0.15f, () =>
            Apply(slot, generation, loadout));
    }

    private void Apply(int slot, int generation, Loadout loadout)
    {
        if (!_active ||
            !_generation.TryGetValue(slot, out var latest) ||
            latest != generation)
            return;

        _generation.Remove(slot);
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player == null || !WeaponUtil.IsAlivePawn(player, out var pawn))
            return;

        WeaponUtil.Give(player, "weapon_knife");
        foreach (var item in loadout.Items)
            WeaponUtil.Give(player, item);

        pawn.ArmorValue = loadout.Armor;
        player.PawnArmor = loadout.Armor;
        player.PawnHasHelmet = loadout.HasHelmet;
        Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_ArmorValue");
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iPawnArmor");
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_bPawnHasHelmet");

        try
        {
            if (pawn.ItemServices != null)
            {
                var items = pawn.ItemServices.As<CCSPlayer_ItemServices>();
                items.HasHelmet = loadout.HasHelmet;
                if (player.Team == CounterStrikeSharp.API.Modules.Utils.CsTeam.CounterTerrorist)
                {
                    items.HasDefuser = loadout.HasDefuser;
                    player.PawnHasDefuser = loadout.HasDefuser;
                    Utilities.SetStateChanged(
                        player,
                        "CCSPlayerController",
                        "m_bPawnHasDefuser");
                }
            }
        }
        catch
        {
            // ignored
        }

        if (!string.IsNullOrEmpty(loadout.ActivePart))
            WeaponUtil.Equip(player, loadout.ActivePart);

        _snapshots[slot] = Capture(player, pawn);
    }

    private static Loadout Capture(
        CCSPlayerController victim,
        CCSPlayerPawn pawn)
    {
        var items = new List<string>();
        var activeName = pawn.WeaponServices?.ActiveWeapon.Value?.DesignerName;
        var weapons = pawn.WeaponServices?.MyWeapons;
        if (weapons != null)
        {
            foreach (var handle in weapons)
            {
                var weapon = handle.Value;
                if (weapon == null || !weapon.IsValid)
                    continue;

                var name = weapon.DesignerName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name) ||
                    WeaponUtil.IsAlwaysKept(name))
                    continue;

                items.Add(name);
            }
        }

        return new Loadout(
            items,
            string.IsNullOrWhiteSpace(activeName)
                ? string.Empty
                : WeaponUtil.NamePartFromDesigner(activeName),
            Math.Clamp(pawn.ArmorValue, 0, 100),
            victim.PawnHasHelmet,
            victim.PawnHasDefuser);
    }

    private static bool IsEnemyKill(
        CCSPlayerController? attacker,
        CCSPlayerController? victim)
    {
        return attacker != null &&
               attacker.IsValid &&
               victim != null &&
               victim.IsValid &&
               attacker.SteamID != victim.SteamID &&
               attacker.Team != victim.Team;
    }

    private sealed record Loadout(
        List<string> Items,
        string ActivePart,
        int Armor,
        bool HasHelmet,
        bool HasDefuser);
}
