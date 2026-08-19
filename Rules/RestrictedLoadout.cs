using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

namespace FunRandomRounds.Rules;

/// <summary>
/// One sequenced "drop everything, then give the rule item" pass per player.
/// Overlapping spawn/start/freeze timers are ignored via a generation token.
/// </summary>
internal sealed class RestrictedLoadout
{
    private readonly FunRandomRoundsPlugin _plugin;
    private readonly string[] _giveItems;
    private readonly string[] _keepParts;
    private readonly Action _applyCvars;
    private readonly Action _restoreCvars;
    private readonly Dictionary<int, int> _generation = new();
    private bool _active;
    private int _epoch;

    public RestrictedLoadout(
        FunRandomRoundsPlugin plugin,
        string[] giveItems,
        string[] keepParts,
        Action applyCvars,
        Action restoreCvars)
    {
        _plugin = plugin;
        _giveItems = giveItems;
        _keepParts = keepParts;
        _applyCvars = applyCvars;
        _restoreCvars = restoreCvars;
    }

    public IReadOnlyList<string> KeepParts => _keepParts;

    public bool IsActive => _active;

    public bool AllowsWeapon(string designerName)
    {
        return !_active || WeaponUtil.IsAllowed(designerName, _keepParts);
    }

    public void Start()
    {
        _epoch++;
        _active = true;
        _generation.Clear();
        LockBuyAndApplySpawnCvars();
        foreach (var player in _plugin.GetCombatPlayers())
            Schedule(player, 0.20f);

        var epoch = _epoch;
        _plugin.AddTimer(1.0f, () =>
        {
            if (_active && _epoch == epoch)
                LockBuyAndApplySpawnCvars();
        });
        _plugin.AddTimer(3.0f, () =>
        {
            if (_active && _epoch == epoch)
                LockBuyAndApplySpawnCvars();
        });
    }

    public void Stop()
    {
        _epoch++;
        _active = false;
        _generation.Clear();
        _restoreCvars();
        UnlockBuy();
    }

    public void OnPlayerSpawn(CCSPlayerController player)
    {
        if (_active)
            Schedule(player, 0.25f);
    }

    public void OnFreezeEnd()
    {
        if (_active)
            LockBuyAndApplySpawnCvars();
    }

    public static void UnlockBuy()
    {
        // mp_buy_allow_guns is a bitfield: 1=pistols only, 255=all. Never leave it at 0/1.
        Server.ExecuteCommand("sv_buy_status_override -1");
        Server.ExecuteCommand("mp_buytime 20");
        Server.ExecuteCommand("mp_buy_anywhere 0");
        Server.ExecuteCommand("mp_buy_during_immunity 0");
        Server.ExecuteCommand("mp_buy_allow_guns 255");
        Server.ExecuteCommand("mp_buy_allow_grenades 1");
        Server.ExecuteCommand("mp_drop_knife_enable 0");
    }

    public static void EnsureBuyUnlocked()
    {
        EnsureCvar("sv_buy_status_override", "-1");
        EnsureCvar("mp_buytime", "20");
        EnsureCvar("mp_buy_anywhere", "0");
        EnsureCvar("mp_buy_during_immunity", "0");
        EnsureCvar("mp_buy_allow_guns", "255");
        EnsureCvar("mp_buy_allow_grenades", "1");
    }

    private static void EnsureCvar(string name, string expected)
    {
        try
        {
            var cvar = ConVar.Find(name);
            if (cvar != null && string.Equals(cvar.StringValue, expected, StringComparison.Ordinal))
                return;
        }
        catch
        {
            // If the value cannot be read, setting the known normal value is safe here.
        }

        Server.ExecuteCommand($"{name} {expected}");
    }

    private void LockBuyAndApplySpawnCvars()
    {
        Server.ExecuteCommand("sv_buy_status_override 3");
        Server.ExecuteCommand("mp_buytime 0");
        Server.ExecuteCommand("mp_buy_anywhere 0");
        Server.ExecuteCommand("mp_buy_during_immunity 0");
        Server.ExecuteCommand("mp_buy_allow_grenades 0");
        _applyCvars();
    }

    private void Schedule(CCSPlayerController player, float delay)
    {
        var slot = player.Slot;
        var gen = _generation.TryGetValue(slot, out var current) ? current + 1 : 1;
        _generation[slot] = gen;
        _plugin.AddTimer(delay, () => Run(slot, gen));
    }

    private void Run(int slot, int gen)
    {
        if (!_active || !_generation.TryGetValue(slot, out var latest) || latest != gen)
            return;

        var player = Utilities.GetPlayerFromSlot(slot);
        if (player == null || !WeaponUtil.IsAlivePawn(player, out _))
            return;

        LockBuyAndApplySpawnCvars();
        WeaponUtil.DropExcept(player, _keepParts);

        _plugin.AddTimer(0.15f, () =>
        {
            if (!_active || !_generation.TryGetValue(slot, out var still) || still != gen)
                return;

            GiveLoadout(slot);
        });
    }

    private void GiveLoadout(int slot)
    {
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player == null || !WeaponUtil.IsAlivePawn(player, out _))
            return;

        foreach (var item in _giveItems)
        {
            var part = WeaponUtil.NamePartFromDesigner(item);
            WeaponUtil.GiveRestricted(_plugin, player, item, part, _keepParts);
            WeaponUtil.Equip(player, part);
        }
    }
}
