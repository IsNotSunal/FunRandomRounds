using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;

namespace FunRandomRounds.Rules;

public sealed class WallhackRule : RoundRule
{
    private readonly Dictionary<int, GlowPair> _glows = new();
    private readonly HashSet<int> _pending = new();
    private bool _active;
    private static readonly int SpottedStructOffset = Schema.GetSchemaOffset("CCSPlayerPawn", "m_entitySpottedState");

    public WallhackRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "黑客来袭";
    public override string Description => "所有人获得透视";

    public override void Start()
    {
        _active = true;
        var delay = 0.20f;
        foreach (var player in Plugin.GetCombatPlayers())
        {
            ScheduleGlow(player, delay);
            delay += 0.05f;
        }
    }

    public override void Stop()
    {
        _active = false;
        _pending.Clear();
        foreach (var slot in _glows.Keys.ToArray())
            RemoveGlow(slot);
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (_active)
            ScheduleGlow(player, 0.20f);
    }

    public override void OnPlayerDeath(EventPlayerDeath @event)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid)
            return;

        _pending.Remove(player.Slot);
        RemoveGlow(player.Slot);
    }

    public override void OnPostEntityThink()
    {
        if (!_active)
            return;

        foreach (var player in Plugin.GetCombatPlayers())
            ForceSpotted(player);
    }

    public override void OnCheckTransmit(CCheckTransmitInfoList infoList)
    {
        if (!_active || _glows.Count == 0)
            return;

        foreach (var (info, viewer) in infoList)
        {
            if (viewer == null || !viewer.IsValid)
                continue;

            foreach (var (slot, pair) in _glows)
            {
                if (!pair.IsValid)
                    continue;

                var target = Utilities.GetPlayerFromSlot(slot);
                if (target == null || !target.IsValid || !target.PawnIsAlive ||
                    slot == viewer.Slot ||
                    viewer.Team == CsTeam.Spectator ||
                    target.Team == CsTeam.Spectator ||
                    target.Team == viewer.Team)
                {
                    info.TransmitEntities.Remove(pair.Relay);
                    info.TransmitEntities.Remove(pair.Glow);
                    continue;
                }

                info.TransmitEntities.Add(pair.Relay);
                info.TransmitEntities.Add(pair.Glow);
            }
        }
    }

    private void ScheduleGlow(CCSPlayerController player, float delay)
    {
        if (!_active || !player.IsValid)
            return;

        var slot = player.Slot;
        _pending.Remove(slot);
        RemoveGlow(slot);

        if (!_pending.Add(slot))
            return;

        Plugin.AddTimer(delay, () =>
        {
            _pending.Remove(slot);
            if (!_active)
                return;

            var owner = Utilities.GetPlayerFromSlot(slot);
            if (owner == null || !owner.IsValid || !owner.PawnIsAlive)
                return;

            RemoveGlow(slot);
            CreateGlow(owner);
        });
    }

    private void CreateGlow(CCSPlayerController player)
    {
        if (_glows.ContainsKey(player.Slot))
            return;

        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid || !player.PawnIsAlive)
            return;

        var model = GetPlayerModel(pawn);
        if (string.IsNullOrWhiteSpace(model) ||
            !model.EndsWith(".vmdl", StringComparison.OrdinalIgnoreCase))
            return;

        var relay = Utilities.CreateEntityByName<CBaseModelEntity>("prop_dynamic");
        var glow = Utilities.CreateEntityByName<CBaseModelEntity>("prop_dynamic");
        if (relay == null || glow == null)
        {
            SafeRemove(relay);
            SafeRemove(glow);
            return;
        }

        try
        {
            relay.Spawnflags = 256;
            relay.Render = Color.Transparent;
            relay.RenderMode = RenderMode_t.kRenderNone;

            glow.Spawnflags = 256;
            glow.Render = Color.FromArgb(1, 0, 0, 0);

            relay.SetModel(model);
            glow.SetModel(model);

            relay.DispatchSpawn();
            glow.DispatchSpawn();

            var color = TeamColor(player);
            glow.Glow.GlowRange = 5000;
            glow.Glow.GlowRangeMin = 0;
            glow.Glow.GlowColorOverride = color;
            glow.Glow.GlowTeam = player.Team == CsTeam.Terrorist
                ? (int)CsTeam.CounterTerrorist
                : (int)CsTeam.Terrorist;
            glow.Glow.GlowType = 3;

            var slot = player.Slot;
            _glows[slot] = new GlowPair(relay, glow);

            Server.NextFrame(() =>
            {
                if (!_active || !_glows.TryGetValue(slot, out var pair) || !pair.IsValid)
                    return;

                var owner = Utilities.GetPlayerFromSlot(slot);
                var livePawn = owner?.PlayerPawn.Value;
                if (owner == null || !owner.IsValid || !owner.PawnIsAlive ||
                    livePawn == null || !livePawn.IsValid)
                {
                    RemoveGlow(slot);
                    return;
                }

                pair.Relay.AcceptInput("FollowEntity", livePawn, pair.Relay, "!activator");
                pair.Glow.AcceptInput("FollowEntity", pair.Relay, pair.Glow, "!activator");
            });
        }
        catch
        {
            SafeRemove(glow);
            SafeRemove(relay);
        }
    }

    private void RemoveGlow(int slot)
    {
        if (!_glows.Remove(slot, out var pair))
            return;

        SafeRemove(pair.Glow);
        SafeRemove(pair.Relay);
    }

    private static void SafeRemove(CBaseModelEntity? entity)
    {
        try
        {
            if (entity != null && entity.IsValid)
                entity.Remove();
        }
        catch
        {
            // ignored
        }
    }

    private static void ForceSpotted(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid)
            return;

        try
        {
            pawn.EntitySpottedState.Spotted = true;
            pawn.EntitySpottedState.SpottedByMask[0] = uint.MaxValue;
            pawn.EntitySpottedState.SpottedByMask[1] = uint.MaxValue;
            Utilities.SetStateChanged(pawn, "EntitySpottedState_t", "m_bSpotted", SpottedStructOffset);
            Utilities.SetStateChanged(pawn, "EntitySpottedState_t", "m_bSpottedByMask", SpottedStructOffset);
        }
        catch
        {
            // ignored
        }
    }

    private static string? GetPlayerModel(CCSPlayerPawn pawn)
    {
        try
        {
            return pawn.CBodyComponent?.SceneNode?.GetSkeletonInstance()?.ModelState.ModelName;
        }
        catch
        {
            return null;
        }
    }

    private static Color TeamColor(CCSPlayerController player)
    {
        return player.Team == CsTeam.Terrorist
            ? Color.FromArgb(255, 255, 48, 48)
            : Color.FromArgb(255, 48, 160, 255);
    }

    private sealed class GlowPair
    {
        public GlowPair(CBaseModelEntity relay, CBaseModelEntity glow)
        {
            Relay = relay;
            Glow = glow;
        }

        public CBaseModelEntity Relay { get; }
        public CBaseModelEntity Glow { get; }

        public bool IsValid =>
            Relay != null && Relay.IsValid && Glow != null && Glow.IsValid;
    }
}
