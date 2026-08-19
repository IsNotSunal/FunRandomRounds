using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace FunRandomRounds.Rules;

public sealed class BhopKingRule : RoundRule
{
    private bool _active;

    public BhopKingRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "身法大王";
    public override string Description => "Auto BunnyHop， 取消限速";

    public override void Start()
    {
        _active = true;
        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("sv_enablebunnyhopping 1");
        Server.ExecuteCommand("sv_autobunnyhopping 1");
        Server.ExecuteCommand("sv_staminamax 0");
        Server.ExecuteCommand("sv_staminalandcost 0");
        Server.ExecuteCommand("sv_staminajumpcost 0");
        Server.ExecuteCommand("sv_staminarecoveryrate 0");
        Server.ExecuteCommand("sv_accelerate_use_weapon_speed 0");
        Server.ExecuteCommand("sv_airaccelerate 2000");
        Server.ExecuteCommand("sv_maxvelocity 7000");
        Server.ExecuteCommand("sv_cheats 0");
    }

    public override void Stop()
    {
        _active = false;
        Server.ExecuteCommand("sv_cheats 1");
        Server.ExecuteCommand("sv_enablebunnyhopping 0");
        Server.ExecuteCommand("sv_autobunnyhopping 0");
        Server.ExecuteCommand("sv_staminamax 80");
        Server.ExecuteCommand("sv_staminalandcost 0.050");
        Server.ExecuteCommand("sv_staminajumpcost 0.080");
        Server.ExecuteCommand("sv_staminarecoveryrate 60");
        Server.ExecuteCommand("sv_accelerate_use_weapon_speed 1");
        Server.ExecuteCommand("sv_airaccelerate 12");
        Server.ExecuteCommand("sv_maxvelocity 3500");
        Server.ExecuteCommand("sv_cheats 0");
    }

    public override void OnTick()
    {
        if (!_active)
            return;

        foreach (var player in Plugin.GetCombatPlayers())
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid)
                continue;

            if ((player.Buttons & PlayerButtons.Jump) == 0)
                continue;

            if ((pawn.Flags & (uint)PlayerFlags.FL_ONGROUND) == 0)
                continue;

            if (pawn.AbsVelocity.Z < 300f)
                pawn.AbsVelocity.Z = 300f;
        }
    }
}
