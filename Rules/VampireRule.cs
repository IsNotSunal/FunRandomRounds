using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace FunRandomRounds.Rules;

public sealed class VampireRule : RoundRule
{
    private const int OverhealCap = 1000;

    public VampireRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "吸血鬼";
    public override string Description => "造成多少伤害获得多少血量";

    public override void Stop()
    {
        foreach (var player in Plugin.GetHumanPlayers())
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid)
                continue;

            pawn.MaxHealth = 100;
            if (pawn.Health > 100)
                pawn.Health = 100;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iMaxHealth");
        }
    }

    public override void OnPlayerHurt(EventPlayerHurt @event)
    {
        var attacker = @event.Attacker;
        var victim = @event.Userid;
        if (attacker == null || !attacker.IsValid || victim == null || !victim.IsValid)
            return;

        if (attacker.SteamID == victim.SteamID)
            return;

        if (attacker.Team == victim.Team)
            return;

        var heal = Math.Max(0, @event.DmgHealth) + Math.Max(0, @event.DmgArmor);
        if (heal <= 0)
            return;

        var pawn = attacker.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid || pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
            return;

        var newHp = Math.Min(OverhealCap, pawn.Health + heal);
        if (newHp <= pawn.Health)
            return;

        if (pawn.MaxHealth < newHp)
        {
            pawn.MaxHealth = newHp;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iMaxHealth");
        }

        pawn.Health = newHp;
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
    }
}
