using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Furni;
using Turbo.Contracts.Rooms;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Object.Logic.Plugins;

/// <summary>
/// The room half of a plugin's furni context (<see cref="IRoomTurn"/>, over
/// <see cref="PluginRoomTurn"/>), the furni's own place in it (the tile in front, the tiles beside)
/// and its scheduled run, which rides the furni's own ticks (<see cref="NextWorkAtMs"/>).
/// </summary>
internal sealed partial class PluginFurniLogic
{
    private PluginRoomTurn? _turn;
    private Func<CancellationToken, Task>? _scheduled;
    private long _scheduledAtMs;

    private PluginRoomTurn Turn =>
        _turn ??= new PluginRoomTurn(_roomGrain, () => _ctx.RoomObject.OwnerId);

    public IReadOnlyList<RoomUser> RoomUsers => Turn.RoomUsers;

    public IRoomMap Map => Turn.Map;

    public Task<bool> SayAsync(
        RoomUser speaker,
        string text,
        RoomSpeech speech,
        CancellationToken ct
    ) => Turn.SayAsync(speaker, text, speech, ct);

    public Task<bool> SetEffectAsync(RoomUser user, int effectId, CancellationToken ct) =>
        Turn.SetEffectAsync(user, effectId, ct);

    public Task<bool> StopWalkingAsync(RoomUser user, CancellationToken ct) =>
        Turn.StopWalkingAsync(user, ct);

    public Task<bool> SetRotationAsync(RoomUser user, Rotation rotation, CancellationToken ct) =>
        Turn.SetRotationAsync(user, rotation, ct);

    public Task<bool> SetBotFigureAsync(RoomUser bot, string figure, CancellationToken ct) =>
        Turn.SetBotFigureAsync(bot, figure, ct);

    public Task<bool> SetBotFollowingAsync(RoomUser bot, RoomUser? target, CancellationToken ct) =>
        Turn.SetBotFollowingAsync(bot, target, ct);

    public Task<RoomFurni?> PlaceTemporaryFurniAsync(
        FurnitureDefinitionSnapshot definition,
        RoomTilePoint tile,
        Rotation rotation,
        CancellationToken ct
    ) => Turn.PlaceTemporaryFurniAsync(definition, tile, rotation, ct);

    public Task<bool> RemoveTemporaryFurniAsync(int furniId, CancellationToken ct) =>
        Turn.RemoveTemporaryFurniAsync(furniId, ct);

    public Task<bool> WalkToInOrderAsync(
        RoomUser user,
        IReadOnlyList<RoomTilePoint> tiles,
        Func<RoomUser, CancellationToken, Task> onArrived,
        CancellationToken ct
    )
    {
        if (!TryGetAvatar(user, out var avatar))
            return Task.FromResult(false);

        var map = MapModule;

        return WalkToAsync(
            avatar,
            tiles
                .Where(tile => map.InBounds(tile.X, tile.Y))
                .Distinct()
                .Select(tile => map.ToIdx(tile.X, tile.Y)),
            (arrived, token) => onArrived(PluginRoomViews.User(arrived), token),
            ct
        );
    }

    public RoomTilePoint? TileInFront =>
        MapModule.TryGetTileInFront(_ctx.GetTileIdx(), _ctx.RoomObject.Rotation, out var idx)
            ? ToPoint(idx)
            : null;

    public IReadOnlyList<RoomTilePoint> TilesBeside(RoomUser user) =>
        TryGetAvatar(user, out var avatar) ? [.. TilesBeside(avatar).Select(ToPoint)] : [];

    public bool StandsBeside(RoomUser user) =>
        TryGetAvatar(user, out var avatar) && StandsBeside(avatar);

    public void Schedule(TimeSpan delay, Func<CancellationToken, Task> run)
    {
        _scheduled = run;
        _scheduledAtMs = NowMs() + (long)Math.Max(0, delay.TotalMilliseconds);

        ScheduleTick();
    }

    public void CancelSchedule() => _scheduled = null;

    protected override long? NextWorkAtMs => _scheduled is null ? null : _scheduledAtMs;

    public override FurnitureUsageType GetUsagePolicy() =>
        Logic.UsagePolicy ?? base.GetUsagePolicy();

    public override bool CanWalk() => Logic.Walkable ?? base.CanWalk();

    public override async Task OnDetachAsync(CancellationToken ct)
    {
        CancelSchedule();

        try
        {
            await Logic.OnDetachedAsync(this, ct);
        }
        finally
        {
            await base.OnDetachAsync(ct);
        }
    }

    /// <summary>Runs the scheduled run once it is due; a run that throws is logged, never the room's tick's end.</summary>
    private async Task RunScheduledAsync(long nowMs, CancellationToken ct)
    {
        if (_scheduled is not { } run || nowMs < _scheduledAtMs)
            return;

        _scheduled = null;

        try
        {
            await run(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError(
                ex,
                "The plugin furni logic {Logic} of item {ItemId} in room {RoomId} failed in a scheduled run",
                Logic.GetType().Name,
                _ctx.ObjectId.Value,
                Room.Value
            );
        }
    }

    private RoomTilePoint ToPoint(int tileIdx)
    {
        var (x, y) = MapModule.GetTileXY(tileIdx);

        return new RoomTilePoint(x, y);
    }
}
