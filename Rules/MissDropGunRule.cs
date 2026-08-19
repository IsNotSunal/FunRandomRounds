using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace FunRandomRounds.Rules;

public sealed class MissDropGunRule : RoundRule
{
    private bool _active;
    private readonly Dictionary<ulong, int> _fireSeq = new();
    private readonly Dictionary<ulong, int> _hitSeq = new();
    private readonly HashSet<ulong> _awaiting = new();

    public MissDropGunRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "豪气冲天";
    public override string Description => "空枪立刻丢掉手中枪械";

    public override void Start()
    {
        _active = true;
        _fireSeq.Clear();
        _hitSeq.Clear();
        _awaiting.Clear();
    }

    public override void Stop()
    {
        _active = false;
        _fireSeq.Clear();
        _hitSeq.Clear();
        _awaiting.Clear();
    }

    public override void OnWeaponFire(EventWeaponFire @event)
    {
        if (!_active)
            return;

        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot || !player.PawnIsAlive)
            return;

        var weapon = @event.Weapon ?? string.Empty;
        if (!IsFirearm(weapon))
            return;

        var steamId = player.SteamID;
        if (_awaiting.Contains(steamId))
        {
            if (!_hitSeq.TryGetValue(steamId, out var hitSeq) ||
                !_fireSeq.TryGetValue(steamId, out var fireSeq) ||
                hitSeq != fireSeq)
            {
                _awaiting.Remove(steamId);
                WeaponUtil.ThrowActiveFirearm(player);
            }

            return;
        }

        if (!_fireSeq.TryGetValue(steamId, out var seq))
            seq = 0;
        seq++;
        _fireSeq[steamId] = seq;
        _awaiting.Add(steamId);
        var mySeq = seq;
        var firedWeapon = weapon;

        // 本帧若打中人会有 player_hurt；没有则下一帧立刻扔枪。
        Server.NextFrame(() => ResolveShot(steamId, mySeq, firedWeapon));
    }

    public override void OnPlayerHurt(EventPlayerHurt @event)
    {
        if (!_active)
            return;

        var attacker = @event.Attacker;
        var victim = @event.Userid;
        if (attacker == null || !attacker.IsValid || attacker.IsBot)
            return;

        if (victim == null || !victim.IsValid)
            return;

        if (attacker.SteamID == victim.SteamID)
            return;

        if (!_fireSeq.TryGetValue(attacker.SteamID, out var seq) || seq <= 0)
            return;

        _hitSeq[attacker.SteamID] = seq;
        _awaiting.Remove(attacker.SteamID);
    }

    private void ResolveShot(ulong steamId, int mySeq, string firedWeapon)
    {
        if (!_active || !_awaiting.Contains(steamId))
            return;

        if (_hitSeq.TryGetValue(steamId, out var hitSeq) && hitSeq == mySeq)
        {
            _awaiting.Remove(steamId);
            return;
        }

        _awaiting.Remove(steamId);
        var shooter = Utilities.GetPlayerFromSteamId(steamId);
        if (shooter == null || !shooter.IsValid || !shooter.PawnIsAlive)
            return;

        WeaponUtil.ThrowActiveFirearm(shooter);
    }

    private static bool IsFirearm(string weapon)
    {
        if (string.IsNullOrWhiteSpace(weapon))
            return false;

        var name = weapon.ToLowerInvariant();
        if (name.Contains("knife") || name.Contains("bayonet") || name.Contains("fists"))
            return false;
        if (name.Contains("grenade") || name.Contains("molotov") || name.Contains("flashbang") ||
            name.Contains("decoy") || name.Contains("incgrenade") || name.Contains("smoke"))
            return false;
        if (name.Contains("c4") || name.Contains("healthshot") || name.Contains("tablet") ||
            name.Contains("breachcharge") || name.Contains("bumpmine"))
            return false;

        return true;
    }
}
