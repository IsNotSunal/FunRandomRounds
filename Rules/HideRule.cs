using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;

namespace FunRandomRounds.Rules;

/// <summary>
/// Hide everyone from everyone else via CheckTransmit. Footsteps, shots, and
/// other player sounds briefly put the pawn back on the wire (labaland/plugin-wallhack).
/// Never hide dead pawns or filter spectators/deathcam — that crashes CS2 clients.
/// </summary>
public sealed class HideRule : RoundRule
{
    private const float RevealSeconds = 2.0f;
    private const float FallNoiseHeight = 44f;
    private static readonly int SpottedStructOffset = Schema.GetSchemaOffset("CCSPlayerPawn", "m_entitySpottedState");

    private readonly Dictionary<int, HideState> _states = new();
    private readonly Dictionary<int, List<int>> _hiddenIndexes = new();
    private bool _active;

    public HideRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "Hide";
    public override string Description => "隐身，但是发出声音就会短暂现形";

    public override void Start()
    {
        _active = true;
        _states.Clear();
        _hiddenIndexes.Clear();
        Server.ExecuteCommand("sv_disable_radar 1");
        Server.ExecuteCommand("mp_radar_showall 0");
        foreach (var player in Plugin.GetCombatPlayers())
            PreparePlayer(player);
    }

    public override void Stop()
    {
        _active = false;
        _hiddenIndexes.Clear();
        foreach (var player in Utilities.GetPlayers())
            RestoreVisuals(player);
        _states.Clear();
        Server.ExecuteCommand("sv_disable_radar 0");
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!_active)
            return;

        var slot = player.Slot;
        Plugin.AddTimer(0.15f, () =>
        {
            if (!_active)
                return;
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p != null)
                PreparePlayer(p);
        });
    }

    public override void OnPlayerDeath(EventPlayerDeath @event)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid)
            return;

        _hiddenIndexes.Remove(player.Slot);
        _states.Remove(player.Slot);
        RestoreVisuals(player);
    }

    public override void OnPlayerHurt(EventPlayerHurt @event)
    {
        Reveal(@event.Userid);
        Reveal(@event.Attacker);
    }

    public override void OnWeaponFire(EventWeaponFire @event) => Reveal(@event.Userid);

    public override void OnPlayerSound(EventPlayerSound @event)
    {
        var duration = RevealSeconds;
        try
        {
            if (@event.Duration > 0)
                duration = Math.Max(0.6f, @event.Duration * 2f);
        }
        catch
        {
            // ignored
        }

        Reveal(@event.Userid, duration);
    }

    public override void OnBulletImpact(EventBulletImpact @event) => Reveal(@event.Userid, 0.5f);

    public override void OnBombBeginPlant(EventBombBeginplant @event) => Reveal(@event.Userid, 1.0f);

    public override void OnBombBeginDefuse(EventBombBegindefuse @event) => Reveal(@event.Userid, 1.0f);

    public override void OnGrenadeThrown(CCSPlayerController player, string weapon) => Reveal(player);

    public override void OnTick()
    {
        if (!_active)
            return;

        _hiddenIndexes.Clear();
        var now = Server.CurrentTime;
        foreach (var player in Plugin.GetCombatPlayers())
        {
            if (!WeaponUtil.IsAlivePawn(player, out var pawn))
            {
                _states.Remove(player.Slot);
                continue;
            }

            var state = GetState(player.Slot);
            ApplyHiddenVisuals(pawn);
            Unspot(pawn);
            TryReloadReveal(pawn, state);
            TryLandingReveal(player, pawn, state);

            if (now <= state.RevealUntil)
                continue;

            var indexes = new List<int> { (int)pawn.Index };
            var weapons = pawn.WeaponServices?.MyWeapons;
            if (weapons != null)
            {
                foreach (var handle in weapons)
                {
                    var weapon = handle.Value;
                    if (weapon != null && weapon.IsValid)
                        indexes.Add((int)weapon.Index);
                }
            }

            _hiddenIndexes[player.Slot] = indexes;
        }
    }

    public override void OnCheckTransmit(CCheckTransmitInfoList infoList)
    {
        if (!_active || _hiddenIndexes.Count == 0)
            return;

        foreach (var (info, viewer) in infoList)
        {
            if (viewer == null || !viewer.IsValid || viewer.IsHLTV || !viewer.PawnIsAlive)
                continue;

            if (viewer.Team is CsTeam.Spectator or CsTeam.None)
                continue;

            foreach (var (slot, indexes) in _hiddenIndexes)
            {
                if (slot == viewer.Slot)
                    continue;

                foreach (var index in indexes)
                    info.TransmitEntities.Remove(index);
            }
        }
    }

    private void PreparePlayer(CCSPlayerController player)
    {
        if (!WeaponUtil.IsAlivePawn(player, out var pawn))
            return;

        GetState(player.Slot);
        ApplyHiddenVisuals(pawn);
        Unspot(pawn);
    }

    private HideState GetState(int slot)
    {
        if (!_states.TryGetValue(slot, out var state))
        {
            state = new HideState();
            _states[slot] = state;
        }

        return state;
    }

    private void Reveal(CCSPlayerController? player, float seconds = RevealSeconds)
    {
        if (!_active || player == null || !player.IsValid)
            return;

        var state = GetState(player.Slot);
        state.RevealUntil = Math.Max(state.RevealUntil, Server.CurrentTime + Math.Max(0.2f, seconds));
    }

    private static void TryReloadReveal(CCSPlayerPawn pawn, HideState state)
    {
        try
        {
            var weapon = pawn.WeaponServices?.ActiveWeapon.Value;
            if (weapon == null || !weapon.IsValid)
            {
                state.Reloading = false;
                return;
            }

            var csWeapon = weapon.As<CCSWeaponBase>();
            if (csWeapon == null || !csWeapon.InReload)
            {
                state.Reloading = false;
                return;
            }

            if (state.Reloading)
                return;

            state.Reloading = true;
            state.RevealUntil = Math.Max(state.RevealUntil, Server.CurrentTime + RevealSeconds);
        }
        catch
        {
            state.Reloading = false;
        }
    }

    private static void TryLandingReveal(CCSPlayerController player, CCSPlayerPawn pawn, HideState state)
    {
        var onGround = (pawn.Flags & (uint)PlayerFlags.FL_ONGROUND) != 0;
        var height = pawn.AbsOrigin?.Z ?? 0f;
        var vertical = Math.Abs(pawn.AbsVelocity?.Z ?? 0f);

        if (!onGround && !state.WasInAir)
        {
            state.WasInAir = true;
            state.FallStartZ = height;
            return;
        }

        if (onGround && state.WasInAir)
        {
            state.WasInAir = false;
            var fall = state.FallStartZ - height;
            if (vertical > 200f || fall > FallNoiseHeight)
                state.RevealUntil = Math.Max(state.RevealUntil, Server.CurrentTime + RevealSeconds);
        }
        else if (!onGround && state.WasInAir && height > state.FallStartZ)
        {
            state.FallStartZ = height;
        }
    }

    private static void ApplyHiddenVisuals(CCSPlayerPawn pawn)
    {
        try
        {
            pawn.ShadowStrength = 0.0f;
            Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_flShadowStrength");
        }
        catch
        {
            // ignored
        }
    }

    private static void RestoreVisuals(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid)
            return;

        try
        {
            pawn.Render = Color.FromArgb(255, pawn.Render);
            Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
            pawn.ShadowStrength = 1.0f;
            Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_flShadowStrength");
        }
        catch
        {
            // ignored
        }

        try
        {
            var weapons = pawn.WeaponServices?.MyWeapons;
            if (weapons == null)
                return;

            foreach (var handle in weapons)
            {
                var weapon = handle.Value;
                if (weapon == null || !weapon.IsValid)
                    continue;

                weapon.Render = Color.FromArgb(255, weapon.Render);
                Utilities.SetStateChanged(weapon, "CBaseModelEntity", "m_clrRender");
                weapon.ShadowStrength = 1.0f;
                Utilities.SetStateChanged(weapon, "CBaseModelEntity", "m_flShadowStrength");
            }
        }
        catch
        {
            // ignored
        }
    }

    private static void Unspot(CCSPlayerPawn pawn)
    {
        try
        {
            pawn.EntitySpottedState.Spotted = false;
            pawn.EntitySpottedState.SpottedByMask[0] = 0;
            pawn.EntitySpottedState.SpottedByMask[1] = 0;
            Utilities.SetStateChanged(pawn, "EntitySpottedState_t", "m_bSpotted", SpottedStructOffset);
            Utilities.SetStateChanged(pawn, "EntitySpottedState_t", "m_bSpottedByMask", SpottedStructOffset);
        }
        catch
        {
            // ignored
        }
    }

    private sealed class HideState
    {
        public float RevealUntil;
        public bool Reloading;
        public bool WasInAir;
        public float FallStartZ;
    }
}
