using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace FunRandomRounds.Rules;

public sealed class ForcedSpreadRule : RoundRule
{
    private const float MinimumSpread = 0.15f;
    private const float MinimumAccuracyPenalty = 1.5f;

    private readonly Dictionary<nint, SavedSpread> _patched = new();
    private bool _active;
    private int _tick;

    public ForcedSpreadRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "马了";
    public override string Description => "所有枪械强制扩散";

    public override void Start()
    {
        _active = true;
        _tick = 0;
        _patched.Clear();
        PatchHeldWeapons();
    }

    public override void Stop()
    {
        _active = false;
        RestoreAll();
        ResetCurrentPenalties();
    }

    public override void OnPlayerSpawn(CCSPlayerController player)
    {
        if (!_active)
            return;

        var slot = player.Slot;
        Plugin.AddTimer(0.25f, () =>
        {
            if (!_active)
                return;

            var current = Utilities.GetPlayerFromSlot(slot);
            if (current != null)
                PatchPlayer(current);
        });
    }

    public override void OnWeaponFire(EventWeaponFire @event)
    {
        if (_active && @event.Userid != null)
            ForceCurrentPenalty(@event.Userid);
    }

    public override void OnTick()
    {
        if (!_active)
            return;

        foreach (var player in Plugin.GetCombatPlayers())
            ForceCurrentPenalty(player);

        _tick++;
        if (_tick % 16 == 0)
            PatchHeldWeapons();
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
            if (weapon == null || !weapon.IsValid ||
                !WeaponUtil.IsFirearmName(weapon.DesignerName ?? string.Empty))
                continue;

            PatchWeapon(weapon);
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

        if (vdata == null || vdata.Handle == nint.Zero || _patched.ContainsKey(vdata.Handle))
            return;

        try
        {
            _patched[vdata.Handle] = SavedSpread.Capture(vdata);
            Raise(vdata.Spread);
            Raise(vdata.InaccuracyCrouch);
            Raise(vdata.InaccuracyStand);
            Raise(vdata.InaccuracyMove);
            Raise(vdata.InaccuracyJump);
            Raise(vdata.InaccuracyLand);
            Raise(vdata.InaccuracyFire);
            vdata.InaccuracyJumpInitial = Math.Max(vdata.InaccuracyJumpInitial, MinimumSpread);
            vdata.InaccuracyJumpApex = Math.Max(vdata.InaccuracyJumpApex, MinimumSpread);
        }
        catch
        {
            _patched.Remove(vdata.Handle);
        }
    }

    private static void ForceCurrentPenalty(CCSPlayerController player)
    {
        if (!WeaponUtil.IsAlivePawn(player, out var pawn))
            return;

        var weapon = pawn.WeaponServices?.ActiveWeapon.Value;
        if (weapon == null || !weapon.IsValid ||
            !WeaponUtil.IsFirearmName(weapon.DesignerName ?? string.Empty))
            return;

        try
        {
            var csWeapon = weapon.As<CCSWeaponBase>();
            csWeapon.AccuracyPenalty = Math.Max(
                csWeapon.AccuracyPenalty,
                MinimumAccuracyPenalty);
            Utilities.SetStateChanged(
                csWeapon,
                "CCSWeaponBase",
                "m_fAccuracyPenalty");
        }
        catch
        {
            // ignored
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

    private static void ResetCurrentPenalties()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            var weapons = player?.PlayerPawn.Value?.WeaponServices?.MyWeapons;
            if (weapons == null)
                continue;

            foreach (var handle in weapons)
            {
                var weapon = handle.Value;
                if (weapon == null || !weapon.IsValid ||
                    !WeaponUtil.IsFirearmName(
                        weapon.DesignerName ?? string.Empty))
                    continue;

                try
                {
                    var csWeapon = weapon.As<CCSWeaponBase>();
                    csWeapon.AccuracyPenalty = 0f;
                    Utilities.SetStateChanged(
                        csWeapon,
                        "CCSWeaponBase",
                        "m_fAccuracyPenalty");
                }
                catch
                {
                    // ignored
                }
            }
        }
    }

    private static void Raise(CFiringModeFloat field)
    {
        var values = field.Values;
        for (var i = 0; i < values.Length; i++)
            values[i] = Math.Max(values[i], MinimumSpread);
    }

    private sealed class SavedSpread
    {
        private readonly CCSWeaponBaseVData _vdata;
        private readonly float[] _spread;
        private readonly float[] _crouch;
        private readonly float[] _stand;
        private readonly float[] _move;
        private readonly float[] _jump;
        private readonly float[] _land;
        private readonly float[] _fire;
        private readonly float _jumpInitial;
        private readonly float _jumpApex;

        private SavedSpread(CCSWeaponBaseVData vdata)
        {
            _vdata = vdata;
            _spread = Copy(vdata.Spread);
            _crouch = Copy(vdata.InaccuracyCrouch);
            _stand = Copy(vdata.InaccuracyStand);
            _move = Copy(vdata.InaccuracyMove);
            _jump = Copy(vdata.InaccuracyJump);
            _land = Copy(vdata.InaccuracyLand);
            _fire = Copy(vdata.InaccuracyFire);
            _jumpInitial = vdata.InaccuracyJumpInitial;
            _jumpApex = vdata.InaccuracyJumpApex;
        }

        public static SavedSpread Capture(CCSWeaponBaseVData vdata) => new(vdata);

        public void Restore()
        {
            Write(_vdata.Spread, _spread);
            Write(_vdata.InaccuracyCrouch, _crouch);
            Write(_vdata.InaccuracyStand, _stand);
            Write(_vdata.InaccuracyMove, _move);
            Write(_vdata.InaccuracyJump, _jump);
            Write(_vdata.InaccuracyLand, _land);
            Write(_vdata.InaccuracyFire, _fire);
            _vdata.InaccuracyJumpInitial = _jumpInitial;
            _vdata.InaccuracyJumpApex = _jumpApex;
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
            var count = Math.Min(values.Length, saved.Length);
            for (var i = 0; i < count; i++)
                values[i] = saved[i];
        }
    }
}
