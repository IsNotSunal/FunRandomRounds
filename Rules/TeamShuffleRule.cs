using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace FunRandomRounds.Rules;

public sealed class TeamShuffleRule : RoundRule
{
    private readonly Dictionary<int, SavedTeam> _originalTeams = new();
    private bool _active;

    public TeamShuffleRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "大洗牌";
    public override string Description => "阵营玩家随机互换（保持两边人数平衡）";

    public override void Start()
    {
        _active = true;
        _originalTeams.Clear();

        var players = Utilities.GetPlayers()
            .Where(player =>
                player != null &&
                player.IsValid &&
                (player.Team == CsTeam.Terrorist ||
                 player.Team == CsTeam.CounterTerrorist))
            .ToList();
        var tCount = players.Count(player => player.Team == CsTeam.Terrorist);
        var ctCount = players.Count(player => player.Team == CsTeam.CounterTerrorist);
        if (tCount == 0 || ctCount == 0)
            return;

        foreach (var player in players)
        {
            _originalTeams[player.Slot] = new SavedTeam(
                player.IsBot ? 0UL : player.SteamID,
                player.Team);
        }

        var fixedT = players
            .Where(HasC4)
            .Take(tCount)
            .ToHashSet();
        var targetT = PickTargetT(players, fixedT, tCount);
        if (targetT == null)
            return;

        foreach (var player in players)
        {
            var target = targetT.Contains(player)
                ? CsTeam.Terrorist
                : CsTeam.CounterTerrorist;
            if (player.Team != target)
                player.SwitchTeam(target);
        }
    }

    public override void Stop()
    {
        if (!_active)
            return;

        _active = false;
        foreach (var (slot, saved) in _originalTeams)
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player == null || !player.IsValid)
                continue;
            if (saved.SteamId != 0 && player.SteamID != saved.SteamId)
                continue;
            if (player.Team != saved.Team)
                player.SwitchTeam(saved.Team);
        }

        _originalTeams.Clear();
    }

    private static HashSet<CCSPlayerController>? PickTargetT(
        List<CCSPlayerController> players,
        HashSet<CCSPlayerController> fixedT,
        int tCount)
    {
        var candidates = players.Where(player => !fixedT.Contains(player)).ToList();
        var needed = tCount - fixedT.Count;

        for (var attempt = 0; attempt < 24; attempt++)
        {
            Shuffle(candidates);
            var target = fixedT.Concat(candidates.Take(needed)).ToHashSet();
            if (players.Any(player =>
                    target.Contains(player) !=
                    (player.Team == CsTeam.Terrorist)))
                return target;
        }

        var currentT = players
            .Where(player =>
                player.Team == CsTeam.Terrorist &&
                !fixedT.Contains(player))
            .ToList();
        var currentCt = players
            .Where(player => player.Team == CsTeam.CounterTerrorist)
            .ToList();
        if (currentT.Count == 0 || currentCt.Count == 0)
            return null;

        var fallback = players
            .Where(player => player.Team == CsTeam.Terrorist)
            .ToHashSet();
        fallback.Remove(currentT[Random.Shared.Next(currentT.Count)]);
        fallback.Add(currentCt[Random.Shared.Next(currentCt.Count)]);
        return fallback;
    }

    private static bool HasC4(CCSPlayerController player)
    {
        return WeaponUtil.IsAlivePawn(player, out var pawn) &&
               WeaponUtil.CountWeapon(pawn, "c4") > 0;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private sealed record SavedTeam(ulong SteamId, CsTeam Team);
}
