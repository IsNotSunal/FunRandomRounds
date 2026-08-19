using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace FunRandomRounds.Rules;

/// <summary>
/// CS2 has no weapon_recoil_scale. Recoil lives on CCSWeaponBase
/// (m_flRecoilIndex / m_iRecoilIndex) and CCSPlayerPawn (m_iShotsFired);
/// movement inaccuracy is weapon_accuracy_nospread + VData InaccuracyMove.
/// </summary>
public sealed class CrossFireRule : RoundRule
{
    private const float RecoilScale = 0.25f;
    private const float MoveSpreadScale = 0.15f;

    private readonly Dictionary<nint, SavedVData> _patched = new();
    private bool _active;
    private int _tick;

    public CrossFireRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "CS2但是CF";
    public override string Description => "移动散射和后坐力大幅度减小";

    public override void Start()
    {
        _active = true;
        _tick = 0;
        _patched.Clear();
        ApplyCvars();
        PatchHeldWeapons();
    }

    public override void Stop()
    {
        _active = false;
        RestoreAll();
        RestoreCvars();
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (_active)
            PatchPlayer(player);
    }

    public override void OnWeaponFire(EventWeaponFire @event)
    {
        if (!_active)
            return;

        var player = @event.Userid;
        if (player != null)
            DampenPlayer(player);
    }

    public override void OnTick()
    {
        if (!_active)
            return;

        foreach (var player in Plugin.GetCombatPlayers())
            DampenPlayer(player);

        _tick++;
        if (_tick % 16 == 0)
            PatchHeldWeapons();
    }

    private static void ApplyCvars()
    {
        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("weapon_accuracy_nospread 1");
        Server.ExecuteCommand("weapon_air_spread_scale 0");
        Server.ExecuteCommand("weapon_accuracy_reset_on_deploy 1");
        Server.ExecuteCommand("sv_strafing_inaccuracy_enabled 0");
        Server.ExecuteCommand("sv_turning_inaccuracy_enabled 0");
        Server.ExecuteCommand("sv_cheats 0");
    }

    private static void RestoreCvars()
    {
        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("weapon_accuracy_nospread 0");
        Server.ExecuteCommand("weapon_air_spread_scale 1");
        Server.ExecuteCommand("weapon_accuracy_reset_on_deploy 0");
        Server.ExecuteCommand("sv_strafing_inaccuracy_enabled 0");
        Server.ExecuteCommand("sv_turning_inaccuracy_enabled 0");
        Server.ExecuteCommand("sv_cheats 0");
    }

    private void PatchHeldWeapons()
    {
        foreach (var player in Plugin.GetCombatPlayers())
            PatchPlayer(player);
    }

    private void PatchPlayer(CCSPlayerController player)
    {
        if (!WeaponUtil.IsAlivePawn(player, out var pawn))
            return;

        var weapons = pawn.WeaponServices?.MyWeapons;
        if (weapons == null)
            return;

        foreach (var handle in weapons)
        {
            var weapon = handle.Value;
            if (weapon != null && weapon.IsValid)
                PatchWeapon(weapon);
        }
    }

    private void DampenPlayer(CCSPlayerController player)
    {
        if (!WeaponUtil.IsAlivePawn(player, out var pawn))
            return;

        try
        {
            if (pawn.ShotsFired > 1)
            {
                pawn.ShotsFired = 1;
                Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_iShotsFired");
            }
        }
        catch
        {
            // ignored
        }

        var active = pawn.WeaponServices?.ActiveWeapon.Value;
        if (active == null || !active.IsValid)
            return;

        DampenWeapon(active);
    }

    private static void DampenWeapon(CBasePlayerWeapon weapon)
    {
        try
        {
            var csWeapon = weapon.As<CCSWeaponBase>();
            csWeapon.AccuracyPenalty = 0f;
            csWeapon.TurningInaccuracy = 0f;
            csWeapon.TurningInaccuracyDelta = 0f;
            csWeapon.FlRecoilIndex *= RecoilScale;
            csWeapon.IRecoilIndex = (int)csWeapon.FlRecoilIndex;
            Utilities.SetStateChanged(csWeapon, "CCSWeaponBase", "m_fAccuracyPenalty");
            Utilities.SetStateChanged(csWeapon, "CCSWeaponBase", "m_flRecoilIndex");
            Utilities.SetStateChanged(csWeapon, "CCSWeaponBase", "m_iRecoilIndex");
        }
        catch
        {
            // ignored
        }
    }

    private void PatchWeapon(CBasePlayerWeapon weapon)
    {
        CCSWeaponBaseVData? vdata;
        try
        {
            vdata = weapon.As<CCSWeaponBase>().VData;
        }
        catch
        {
            return;
        }

        if (vdata == null || vdata.Handle == nint.Zero)
            return;

        if (_patched.ContainsKey(vdata.Handle))
            return;

        try
        {
            _patched[vdata.Handle] = SavedVData.Capture(vdata);
            Scale(vdata.RecoilMagnitude, RecoilScale);
            Scale(vdata.RecoilMagnitudeVariance, RecoilScale);
            Scale(vdata.RecoilAngleVariance, RecoilScale);
            Scale(vdata.InaccuracyMove, MoveSpreadScale);
            Scale(vdata.InaccuracyJump, MoveSpreadScale);
            Scale(vdata.InaccuracyLand, MoveSpreadScale);
            Scale(vdata.InaccuracyFire, RecoilScale);
            vdata.InaccuracyJumpInitial *= MoveSpreadScale;
            vdata.InaccuracyJumpApex *= MoveSpreadScale;
            vdata.InaccuracyReload *= RecoilScale;
            vdata.RecoveryTimeStand *= RecoilScale;
            vdata.RecoveryTimeCrouch *= RecoilScale;
            vdata.RecoveryTimeStandFinal *= RecoilScale;
            vdata.RecoveryTimeCrouchFinal *= RecoilScale;
        }
        catch
        {
            _patched.Remove(vdata.Handle);
        }
    }

    private void RestoreAll()
    {
        foreach (var saved in _patched.Values)
        {
            try
            {
                saved.Restore();
            }
            catch
            {
                // ignored
            }
        }

        _patched.Clear();
    }

    private static void Scale(CFiringModeFloat field, float scale)
    {
        var values = field.Values;
        for (var i = 0; i < values.Length; i++)
            values[i] *= scale;
    }

    private sealed class SavedVData
    {
        private readonly CCSWeaponBaseVData _vdata;
        private readonly float[] _recoilMag;
        private readonly float[] _recoilMagVar;
        private readonly float[] _recoilAngleVar;
        private readonly float[] _move;
        private readonly float[] _jump;
        private readonly float[] _land;
        private readonly float[] _fire;
        private readonly float _jumpInitial;
        private readonly float _jumpApex;
        private readonly float _reload;
        private readonly float _recoverStand;
        private readonly float _recoverCrouch;
        private readonly float _recoverStandFinal;
        private readonly float _recoverCrouchFinal;

        private SavedVData(CCSWeaponBaseVData vdata)
        {
            _vdata = vdata;
            _recoilMag = Copy(vdata.RecoilMagnitude);
            _recoilMagVar = Copy(vdata.RecoilMagnitudeVariance);
            _recoilAngleVar = Copy(vdata.RecoilAngleVariance);
            _move = Copy(vdata.InaccuracyMove);
            _jump = Copy(vdata.InaccuracyJump);
            _land = Copy(vdata.InaccuracyLand);
            _fire = Copy(vdata.InaccuracyFire);
            _jumpInitial = vdata.InaccuracyJumpInitial;
            _jumpApex = vdata.InaccuracyJumpApex;
            _reload = vdata.InaccuracyReload;
            _recoverStand = vdata.RecoveryTimeStand;
            _recoverCrouch = vdata.RecoveryTimeCrouch;
            _recoverStandFinal = vdata.RecoveryTimeStandFinal;
            _recoverCrouchFinal = vdata.RecoveryTimeCrouchFinal;
        }

        public static SavedVData Capture(CCSWeaponBaseVData vdata) => new(vdata);

        public void Restore()
        {
            Write(_vdata.RecoilMagnitude, _recoilMag);
            Write(_vdata.RecoilMagnitudeVariance, _recoilMagVar);
            Write(_vdata.RecoilAngleVariance, _recoilAngleVar);
            Write(_vdata.InaccuracyMove, _move);
            Write(_vdata.InaccuracyJump, _jump);
            Write(_vdata.InaccuracyLand, _land);
            Write(_vdata.InaccuracyFire, _fire);
            _vdata.InaccuracyJumpInitial = _jumpInitial;
            _vdata.InaccuracyJumpApex = _jumpApex;
            _vdata.InaccuracyReload = _reload;
            _vdata.RecoveryTimeStand = _recoverStand;
            _vdata.RecoveryTimeCrouch = _recoverCrouch;
            _vdata.RecoveryTimeStandFinal = _recoverStandFinal;
            _vdata.RecoveryTimeCrouchFinal = _recoverCrouchFinal;
        }

        private static float[] Copy(CFiringModeFloat field)
        {
            var values = field.Values;
            var copy = new float[values.Length];
            values.CopyTo(copy);
            return copy;
        }

        private static void Write(CFiringModeFloat field, float[] saved)
        {
            var values = field.Values;
            var n = Math.Min(values.Length, saved.Length);
            for (var i = 0; i < n; i++)
                values[i] = saved[i];
        }
    }
}
