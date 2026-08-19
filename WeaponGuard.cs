using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace FunRandomRounds;

internal sealed class WeaponGuard
{
    private readonly FunRandomRoundsPlugin _plugin;
    private readonly Func<DynamicHook, HookResult> _onCanAcquire;

    public WeaponGuard(FunRandomRoundsPlugin plugin)
    {
        _plugin = plugin;
        _onCanAcquire = OnCanAcquire;
    }

    public void Hook()
    {
        VirtualFunctions.CCSPlayer_ItemServices_CanAcquireFunc.Hook(_onCanAcquire, HookMode.Pre);
    }

    public void Unhook()
    {
        try
        {
            VirtualFunctions.CCSPlayer_ItemServices_CanAcquireFunc.Unhook(_onCanAcquire, HookMode.Pre);
        }
        catch
        {
            // ignored
        }
    }

    private HookResult OnCanAcquire(DynamicHook hook)
    {
        if (!_plugin.BlocksRestrictedWeapons || _plugin.IsInternalGive)
            return HookResult.Continue;

        try
        {
            var item = hook.GetParam<CEconItemView>(1);
            var name = DesignerNameFromItem(item);
            if (string.IsNullOrEmpty(name) || _plugin.AllowsRestrictedWeapon(name))
                return HookResult.Continue;

            var method = hook.GetParam<int>(2);
            hook.SetReturn(method == 1 ? 9 : 8);
            return HookResult.Handled;
        }
        catch
        {
            return HookResult.Continue;
        }
    }

    private static string DesignerNameFromItem(CEconItemView item)
    {
        try
        {
            var data = VirtualFunctions.GetCSWeaponDataFromKeyFunc.Invoke(-1, item.ItemDefinitionIndex.ToString());
            if (data == null)
                return string.Empty;

            return data.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
