using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using FunRandomRounds.Rules;

namespace FunRandomRounds;

public class FunRandomRoundsPlugin : BasePlugin
{
    public override string ModuleName => "FunRandomRounds";
    public override string ModuleVersion => "1.5.8";
    public override string ModuleAuthor => "CS2Server";
    public override string ModuleDescription => "MatchZy companion: random rule each round";

    private const string Prefix = " \x04[随机规则]\x01 ";
    private const float CenterBannerSeconds = 10f;

    private bool _enabled;
    private List<RoundRule> _rules = [];
    private RoundRule? _current;
    private int _lastIndex = -1;
    private readonly HashSet<int> _usedRuleIndices = [];
    private int? _forcedIndex;
    private readonly Random _random = new();
    private readonly CvarSnapshot _cvars = new();
    private WeaponGuard? _weaponGuard;
    private int _pendingApply;
    private int _internalGiveDepth;
    private int _buyStateTick;
    private string? _centerBannerHtml;
    private float _centerBannerUntil;

    public override void Load(bool hotReload)
    {
        _rules = RuleRegistry.CreateAll(this);

        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterListener<Listeners.OnServerPostEntityThink>(OnPostEntityThink);
        RegisterListener<Listeners.CheckTransmit>(OnCheckTransmit);

        AddCommandListener("say", OnSay);
        AddCommandListener("say_team", OnSay);
        AddCommandListener("buy", OnBuyCommand);
        AddCommandListener("buyammo1", OnBuyCommand);
        AddCommandListener("buyammo2", OnBuyCommand);
        AddCommandListener("autobuy", OnBuyCommand);
        AddCommandListener("rebuy", OnBuyCommand);

        _weaponGuard = new WeaponGuard(this);
        _weaponGuard.Hook();

        if (hotReload)
            Disable(announce: false);
    }

    public override void Unload(bool hotReload)
    {
        Disable(announce: false);
        _weaponGuard?.Unhook();
    }

    public bool BlocksRestrictedWeapons => _enabled && (_current?.BlocksBuyAndPickup ?? false);

    public bool IsInternalGive => _internalGiveDepth > 0;

    public void BeginInternalGive() => _internalGiveDepth++;

    public void EndInternalGive()
    {
        if (_internalGiveDepth > 0)
            _internalGiveDepth--;
    }

    public bool AllowsRestrictedWeapon(string designerName)
    {
        if (_current == null)
            return true;
        return _current.AllowsWeapon(designerName);
    }

    private HookResult OnBuyCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (_enabled && (_current?.BlocksBuy == true || BlocksRestrictedWeapons))
            return HookResult.Handled;
        return HookResult.Continue;
    }

    [ConsoleCommand("css_funrand", "开关随机规则模式。css_funrand [off|list|set <编号>]")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void OnFunCommand(CCSPlayerController? player, CommandInfo info)
    {
        var arg = info.ArgCount > 1 ? info.ArgByIndex(1) : string.Empty;
        var extra = info.ArgCount > 2 ? info.ArgByIndex(2) : string.Empty;
        HandleCommand(player, arg, extra);
    }

    private HookResult OnSay(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var text = info.ArgCount > 1 ? info.GetArg(1).Trim() : string.Empty;
        if (string.IsNullOrEmpty(text) || text[0] != '.')
            return HookResult.Continue;

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        if (cmd is not (".funrand" or ".rand" or ".random"))
            return HookResult.Continue;

        HandleCommand(player, parts.Length > 1 ? parts[1] : string.Empty, parts.Length > 2 ? parts[2] : string.Empty);
        return HookResult.Handled;
    }

    private void HandleCommand(CCSPlayerController? player, string arg, string extra)
    {
        arg = arg.Trim().ToLowerInvariant();

        if (arg is "list" or "ls")
        {
            Reply(player, "规则列表：");
            for (var i = 0; i < _rules.Count; i++)
                Reply(player, $"{i + 1}. {_rules[i].Name} — {_rules[i].Description}");
            return;
        }

        if (player != null && !IsAdmin(player))
        {
            Reply(player, "只有管理员可以开关随机规则模式。");
            return;
        }

        if (arg is "off" or "0" or "disable")
        {
            Disable(announce: true);
            return;
        }

        if (arg is "set" or "force")
        {
            if (!TryParseRuleIndex(extra, out var index))
            {
                Reply(player, "用法：.funrand set <编号>，先用 .funrand list 查看。");
                return;
            }

            _forcedIndex = index;
            BeginMode();
            CancelPendingApply();
            if (IsWarmup())
            {
                StopCurrentRule();
                Reply(player, $"热身中已指定，开赛重启后生效：{_rules[index].Name}");
                return;
            }

            ApplyRule(index);
            Reply(player, $"已指定本回合规则：{_rules[index].Name}");
            return;
        }

        if (int.TryParse(arg, out _) && TryParseRuleIndex(arg, out var directIndex))
        {
            _forcedIndex = directIndex;
            BeginMode();
            CancelPendingApply();
            if (IsWarmup())
            {
                StopCurrentRule();
                Reply(player, $"热身中已指定，开赛重启后生效：{_rules[directIndex].Name}");
                return;
            }

            ApplyRule(directIndex);
            Reply(player, $"已指定本回合规则：{_rules[directIndex].Name}");
            return;
        }

        if (_enabled)
        {
            Reply(player, "随机规则模式已在运行。聊天输入 .funrand off 关闭，.funrand list 查看规则。");
            return;
        }

        BeginMode();
        Server.PrintToChatAll($"{Prefix}随机规则模式已开启：正常竞技，每回合随机一条规则。");
        Server.PrintToChatAll($"{Prefix}仍用 MatchZy 开赛：.ready 或管理员 .start");
        Server.PrintToChatAll($"{Prefix}请不要同时开 .fun1v5。");

        if (!IsWarmup())
            PickAndApply();
    }

    private void BeginMode()
    {
        if (_enabled)
            return;

        Server.ExecuteCommand("css_fun1v5 off");
        _cvars.Capture();
        _usedRuleIndices.Clear();
        _enabled = true;
    }

    private void Disable(bool announce)
    {
        var wasEnabled = _enabled;
        _enabled = false;
        _forcedIndex = null;
        _centerBannerHtml = null;
        CancelPendingApply();
        StopCurrentRule();
        _lastIndex = -1;
        _usedRuleIndices.Clear();
        if (wasEnabled)
        {
            _cvars.Restore();
            ResetPlayers();
        }

        if (announce)
            Server.PrintToChatAll($"{Prefix}随机规则模式已关闭，已还原娱乐模式开启前的服务器设置。");
    }

    private static void ResetPlayers()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (player == null || !player.IsValid)
                continue;

            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !pawn.IsValid)
                continue;

            pawn.MaxHealth = 100;
            if (pawn.LifeState == (byte)LifeState_t.LIFE_ALIVE)
                pawn.Health = 100;
            if (pawn.ArmorValue > 100)
                pawn.ArmorValue = 100;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iMaxHealth");
            Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_ArmorValue");
        }
    }

    private void StopCurrentRule()
    {
        try
        {
            _current?.Stop();
        }
        finally
        {
            _current = null;
            RestrictedLoadout.UnlockBuy();
        }
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (!_enabled)
            return HookResult.Continue;

        // Stop before the next round's spawn, otherwise the old rule (e.g. grenade-only)
        // still strips weapons on player_spawn. Also runs before MatchZy mp_restartgame.
        StopCurrentRule();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundPrestart(EventRoundPrestart @event, GameEventInfo info)
    {
        if (!_enabled)
            return HookResult.Continue;

        // mp_restartgame can spawn players without a clean round_end. Unlock buy and
        // stop restricted loadouts before the restart spawn.
        StopCurrentRule();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (!_enabled || IsWarmup())
            return HookResult.Continue;

        // MatchZy prints LIVE and runs mp_restartgame several times. Each restart
        // fires round_start; applying a new rule every time strips players empty.
        QueuePickAndApply(2.5f);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
    {
        if (!_enabled)
            return HookResult.Continue;

        StopCurrentRule();
        _usedRuleIndices.Clear();
        _lastIndex = -1;
        Server.PrintToChatAll($"{Prefix}热身结束，开赛重启后开始随机规则。");
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
    {
        if (_enabled)
            _current?.OnFreezeEnd();
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (!_enabled)
            return HookResult.Continue;

        var player = @event.Userid;
        if (player != null && player.IsValid)
            _current?.OnPlayerSpawn(player);

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (_enabled)
            _current?.OnPlayerHurt(@event);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (_enabled)
            _current?.OnPlayerDeath(@event);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
    {
        if (!_enabled)
            return HookResult.Continue;

        var player = @event.Userid;
        if (player != null && player.IsValid)
            _current?.OnGrenadeThrown(player, @event.Weapon ?? string.Empty);

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        if (!_enabled)
            return HookResult.Continue;

        _current?.OnWeaponFire(@event);

        var weapon = @event.Weapon ?? string.Empty;
        if (!WeaponUtil.IsUtilityName(weapon))
            return HookResult.Continue;

        var player = @event.Userid;
        if (player != null && player.IsValid)
            _current?.OnGrenadeThrown(player, weapon);

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSound(EventPlayerSound @event, GameEventInfo info)
    {
        if (_enabled)
            _current?.OnPlayerSound(@event);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBulletImpact(EventBulletImpact @event, GameEventInfo info)
    {
        if (_enabled)
            _current?.OnBulletImpact(@event);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombBeginPlant(EventBombBeginplant @event, GameEventInfo info)
    {
        if (_enabled)
            _current?.OnBombBeginPlant(@event);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnBombBeginDefuse(EventBombBegindefuse @event, GameEventInfo info)
    {
        if (_enabled)
            _current?.OnBombBeginDefuse(@event);
        return HookResult.Continue;
    }

    private void OnTick()
    {
        if (_enabled)
        {
            _current?.OnTick();
            _buyStateTick++;
            if (_buyStateTick >= 64)
            {
                _buyStateTick = 0;
                if (_current?.BlocksBuy != true)
                    RestrictedLoadout.EnsureBuyUnlocked();
            }
        }

        RefreshCenterBanner();
    }

    private void RefreshCenterBanner()
    {
        if (_centerBannerHtml == null)
            return;

        if (Server.CurrentTime >= _centerBannerUntil)
        {
            _centerBannerHtml = null;
            return;
        }

        foreach (var player in Utilities.GetPlayers())
        {
            if (player == null || !player.IsValid || player.IsBot)
                continue;

            try
            {
                player.PrintToCenterHtml(_centerBannerHtml);
            }
            catch
            {
                // ignored
            }
        }
    }

    private void OnPostEntityThink()
    {
        if (_enabled)
            _current?.OnPostEntityThink();
    }

    private void OnCheckTransmit(CCheckTransmitInfoList infoList)
    {
        if (_enabled)
            _current?.OnCheckTransmit(infoList);
    }

    private void CancelPendingApply() => _pendingApply++;

    private void QueuePickAndApply(float delay)
    {
        var token = ++_pendingApply;
        AddTimer(delay, () =>
        {
            if (!_enabled || token != _pendingApply || IsWarmup())
                return;
            PickAndApply();
        });
    }

    private void PickAndApply()
    {
        int index;
        if (_forcedIndex.HasValue)
        {
            index = _forcedIndex.Value;
            _forcedIndex = null;
        }
        else
        {
            index = PickRandomIndex();
        }

        if (index < 0)
        {
            StopCurrentRule();
            Server.PrintToChatAll($"{Prefix}本场比赛的所有规则均已使用，本回合按正常规则进行。");
            return;
        }

        ApplyRule(index);
    }

    private int PickRandomIndex()
    {
        var available = Enumerable.Range(0, _rules.Count)
            .Where(index => !_usedRuleIndices.Contains(index))
            .ToArray();

        return available.Length == 0
            ? -1
            : available[_random.Next(available.Length)];
    }

    private void ApplyRule(int index)
    {
        if (index < 0 || index >= _rules.Count)
            return;

        StopCurrentRule();
        _current = _rules[index];
        _lastIndex = index;
        _usedRuleIndices.Add(index);
        _current.Start();
        BroadcastRule(_current);
    }

    private void BroadcastRule(RoundRule rule)
    {
        Server.PrintToChatAll($" \x10{rule.Name}");
        Server.PrintToChatAll($" \x01{rule.Description}");

        _centerBannerHtml =
            $"<b><font color='#FFD700'>{rule.Name}</font></b><br/>{rule.Description}";
        _centerBannerUntil = Server.CurrentTime + CenterBannerSeconds;
        RefreshCenterBanner();
    }

    public IEnumerable<CCSPlayerController> GetHumanPlayers()
    {
        return Utilities.GetPlayers().Where(p =>
            p != null && p.IsValid && !p.IsBot && p.Connected == PlayerConnectedState.PlayerConnected);
    }

    public IEnumerable<CCSPlayerController> GetCombatPlayers()
    {
        return Utilities.GetPlayers().Where(p =>
            p != null && p.IsValid &&
            p.PawnIsAlive &&
            (p.Team == CsTeam.Terrorist || p.Team == CsTeam.CounterTerrorist));
    }

    private bool TryParseRuleIndex(string text, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (int.TryParse(text.Trim(), out var number) && number >= 1 && number <= _rules.Count)
        {
            index = number - 1;
            return true;
        }

        var match = _rules.FindIndex(r => r.Name.Equals(text.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match < 0)
            return false;

        index = match;
        return true;
    }

    private static bool IsWarmup()
    {
        var proxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
        return proxy?.GameRules?.WarmupPeriod == true;
    }

    private bool IsAdmin(CCSPlayerController player)
    {
        try
        {
            if (AdminManager.PlayerHasPermissions(player, "@css/generic") ||
                AdminManager.PlayerHasPermissions(player, "@css/root"))
                return true;
        }
        catch
        {
            // ignored
        }

        try
        {
            var path = Path.Combine(Server.GameDirectory, "csgo", "cfg", "MatchZy", "admins.json");
            if (!File.Exists(path))
                return false;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var steamId = player.SteamID.ToString();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == steamId)
                    return true;
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }

    private static void Reply(CCSPlayerController? player, string message)
    {
        if (player == null || !player.IsValid)
            Server.PrintToConsole($"[随机规则] {message}");
        else
            player.PrintToChat($"{Prefix}{message}");
    }
}
