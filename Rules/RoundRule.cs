using CounterStrikeSharp.API.Core;

namespace FunRandomRounds.Rules;

public abstract class RoundRule
{
    protected FunRandomRoundsPlugin Plugin { get; }

    protected RoundRule(FunRandomRoundsPlugin plugin)
    {
        Plugin = plugin;
    }

    public abstract string Name { get; }
    public abstract string Description { get; }

    public virtual void Start()
    {
    }

    public virtual void Stop()
    {
    }

    public virtual void OnPlayerSpawn(CCSPlayerController player)
    {
    }

    public virtual void OnPlayerHurt(EventPlayerHurt @event)
    {
    }

    public virtual void OnPlayerDeath(EventPlayerDeath @event)
    {
    }

    public virtual void OnGrenadeThrown(CCSPlayerController player, string weapon)
    {
    }

    public virtual void OnWeaponFire(EventWeaponFire @event)
    {
    }

    public virtual void OnPlayerSound(EventPlayerSound @event)
    {
    }

    public virtual void OnBulletImpact(EventBulletImpact @event)
    {
    }

    public virtual void OnBombBeginPlant(EventBombBeginplant @event)
    {
    }

    public virtual void OnBombBeginDefuse(EventBombBegindefuse @event)
    {
    }

    public virtual void OnFreezeEnd()
    {
    }

    public virtual void OnTick()
    {
    }

    public virtual void OnPostEntityThink()
    {
    }

    public virtual void OnCheckTransmit(CCheckTransmitInfoList infoList)
    {
    }

    public virtual bool BlocksBuyAndPickup => false;

    public virtual bool BlocksBuy => false;

    public virtual bool AllowsWeapon(string designerName) => true;

    public virtual HookResult OnClientSelectSlot(CCSPlayerController player, string command)
        => HookResult.Continue;
}
