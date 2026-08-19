using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace FunRandomRounds.Rules;

public sealed class KillTeleportRule : RoundRule
{
    public KillTeleportRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "击杀传送";
    public override string Description => "在击杀敌人时传送到敌人位置";

    public override void OnPlayerDeath(EventPlayerDeath @event)
    {
        var victim = @event.Userid;
        var attacker = @event.Attacker;
        if (victim == null || !victim.IsValid || attacker == null || !attacker.IsValid)
            return;

        if (attacker.SteamID == victim.SteamID)
            return;

        if (attacker.Team == victim.Team)
            return;

        var victimPawn = victim.PlayerPawn.Value;
        var attackerPawn = attacker.PlayerPawn.Value;
        if (victimPawn == null || !victimPawn.IsValid || attackerPawn == null || !attackerPawn.IsValid)
            return;

        if (attackerPawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return;

        var origin = victimPawn.AbsOrigin;
        if (origin == null)
            return;

        var x = origin.X;
        var y = origin.Y;
        var z = origin.Z;
        var angles = victimPawn.EyeAngles;
        var pitch = angles?.X ?? 0f;
        var yaw = angles?.Y ?? 0f;
        var roll = angles?.Z ?? 0f;
        var steamId = attacker.SteamID;

        Server.NextFrame(() =>
        {
            var player = Utilities.GetPlayerFromSteamId(steamId);
            var pawn = player?.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
                return;

            pawn.Teleport(
                new Vector(x, y, z),
                new QAngle(pitch, yaw, roll),
                new Vector(0, 0, 0));
        });
    }
}
