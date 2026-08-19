using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace FunRandomRounds.Rules;

public sealed class SpawnShuffleRule : RoundRule
{
    private bool _active;
    private int _epoch;

    public SpawnShuffleRule(FunRandomRoundsPlugin plugin) : base(plugin)
    {
    }

    public override string Name => "内鬼？！！！";
    public override string Description => "出生位置玩家位置随机调换";

    public override void Start()
    {
        _active = true;
        _epoch++;

        var players = Plugin.GetCombatPlayers()
            .Select(Capture)
            .Where(state => state != null)
            .Cast<PlayerPosition>()
            .ToList();
        if (players.Count < 2)
            return;

        Shuffle(players);
        var shift = Random.Shared.Next(1, players.Count);
        var epoch = _epoch;

        Server.NextFrame(() =>
        {
            if (!_active || epoch != _epoch)
                return;

            for (var i = 0; i < players.Count; i++)
            {
                var player = Resolve(players[i]);
                var pawn = player?.PlayerPawn.Value;
                if (pawn == null || !pawn.IsValid ||
                    pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE)
                    continue;

                var destination = players[(i + shift) % players.Count];
                pawn.Teleport(
                    new Vector(
                        destination.Position.X,
                        destination.Position.Y,
                        destination.Position.Z),
                    new QAngle(
                        destination.Angles.X,
                        destination.Angles.Y,
                        destination.Angles.Z),
                    new Vector(0, 0, 0));
            }
        });
    }

    public override void Stop()
    {
        _active = false;
        _epoch++;
    }

    private static PlayerPosition? Capture(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn.Value;
        var origin = pawn?.AbsOrigin;
        if (pawn == null || !pawn.IsValid || origin == null)
            return null;

        var angles = pawn.EyeAngles;
        return new PlayerPosition(
            player.Slot,
            player.IsBot ? 0UL : player.SteamID,
            new Vector(origin.X, origin.Y, origin.Z),
            new QAngle(
                angles?.X ?? 0f,
                angles?.Y ?? 0f,
                angles?.Z ?? 0f));
    }

    private static CCSPlayerController? Resolve(PlayerPosition state)
    {
        var player = Utilities.GetPlayerFromSlot(state.Slot);
        if (player == null || !player.IsValid)
            return null;

        if (state.SteamId != 0 && player.SteamID != state.SteamId)
            return null;

        return player;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private sealed record PlayerPosition(
        int Slot,
        ulong SteamId,
        Vector Position,
        QAngle Angles);
}
