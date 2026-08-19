using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace FunRandomRounds.Rules;

public sealed class SmallPlayerRule : RoundRule
{
    private const float PlayerScale = 0.5f;
    private const float StandingViewHeight = 64.0f;
    private const float CrouchingViewHeight = 46.0f;

    private bool _active;
    private int _epoch;

    public SmallPlayerRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "我是卡莎！";
    public override string Description => "人物缩小";

    public override void Start()
    {
        _active = true;
        _epoch++;

        foreach (var player in Plugin.GetCombatPlayers())
            ScheduleApply(player);
    }

    public override void Stop()
    {
        _active = false;
        _epoch++;

        foreach (var player in Plugin.GetHumanPlayers())
            RestorePlayer(player);
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (_active)
            ScheduleApply(player);
    }

    public override void OnTick()
    {
        if (!_active)
            return;

        foreach (var player in Plugin.GetCombatPlayers())
        {
            if (!WeaponUtil.IsAlivePawn(player, out var pawn))
                continue;

            UpdateViewHeight(pawn, PlayerScale);
        }
    }

    private void ScheduleApply(CCSPlayerController player)
    {
        var slot = player.Slot;
        var epoch = _epoch;
        Plugin.AddTimer(0.20f, () =>
        {
            if (!_active || epoch != _epoch)
                return;

            var current = Utilities.GetPlayerFromSlot(slot);
            if (current != null)
                ApplyPlayer(current);
        });
    }

    private static void ApplyPlayer(CCSPlayerController player)
    {
        if (!WeaponUtil.IsAlivePawn(player, out var pawn))
            return;

        SetScale(pawn, PlayerScale);
        UpdateViewHeight(pawn, PlayerScale);
    }

    private static void RestorePlayer(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid)
            return;

        SetScale(pawn, 1.0f);
        UpdateViewHeight(pawn, 1.0f);
    }

    private static void SetScale(CCSPlayerPawn pawn, float scale)
    {
        try
        {
            var sceneNode = pawn.CBodyComponent?.SceneNode;
            if (sceneNode == null)
                return;

            sceneNode.GetSkeletonInstance().Scale = scale;
            pawn.AcceptInput(
                "SetScale",
                pawn,
                pawn,
                scale.ToString(CultureInfo.InvariantCulture));

            Server.NextFrame(() =>
            {
                if (pawn.IsValid)
                    Utilities.SetStateChanged(pawn, "CBaseEntity", "m_CBodyComponent");
            });
        }
        catch
        {
            // ignored
        }
    }

    private static void UpdateViewHeight(CCSPlayerPawn pawn, float scale)
    {
        try
        {
            var isCrouching =
                (pawn.Flags & (uint)PlayerFlags.FL_DUCKING) != 0;
            var normalHeight = isCrouching
                ? CrouchingViewHeight
                : StandingViewHeight;
            var targetHeight = normalHeight * scale;

            if (Math.Abs(pawn.ViewOffset.Z - targetHeight) < 0.01f)
                return;

            pawn.ViewOffset.Z = targetHeight;
            Utilities.SetStateChanged(
                pawn,
                "CBaseModelEntity",
                "m_vecViewOffset");
        }
        catch
        {
            // ignored
        }
    }
}
