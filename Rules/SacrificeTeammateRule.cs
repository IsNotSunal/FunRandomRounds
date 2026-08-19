using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace FunRandomRounds.Rules;

public sealed class SacrificeTeammateRule : RoundRule
{
    private const int OverhealCap = 1000;

    public SacrificeTeammateRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "献祭队友";
    public override string Description => "打队友回血";

    public override void Start()
    {
        Server.ExecuteCommand("mp_friendlyfire 1");
        Server.ExecuteCommand("ff_damage_reduction_bullets 1");
        Server.ExecuteCommand("ff_damage_reduction_grenade 1");
        Server.ExecuteCommand("ff_damage_reduction_other 1");
        Server.ExecuteCommand("mp_autokick 0");
        Server.ExecuteCommand("mp_tkpunish 0");
    }

    public override void Stop()
    {
        Server.ExecuteCommand("mp_friendlyfire 0");
        Server.ExecuteCommand("ff_damage_reduction_bullets 0.33");
        Server.ExecuteCommand("ff_damage_reduction_grenade 0.25");
        Server.ExecuteCommand("ff_damage_reduction_other 0.4");
        Server.ExecuteCommand("mp_autokick 1");
        Server.ExecuteCommand("mp_tkpunish 0");

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

        if (attacker.Team != victim.Team)
            return;

        if (attacker.Team is not (CsTeam.Terrorist or CsTeam.CounterTerrorist))
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
