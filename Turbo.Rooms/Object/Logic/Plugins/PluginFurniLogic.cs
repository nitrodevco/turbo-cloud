using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Furni;
using Turbo.Contracts.Rooms;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Object.Logic.Plugins;

/// <summary>
/// A plugin's <see cref="FurniLogic"/> run as a floor furni logic of core's: each hook runs after
/// core's own (a walk-on still reaches wired), and a use the plugin does not handle does what the
/// furni does by default. Its walks are core's walk-to-use machinery
/// (<see cref="FurnitureWalkToUseLogic"/>), with the plugin's callback as the arrival.
/// </summary>
internal sealed class PluginFurniLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx,
    FurniLogic logic,
    bool linkedPair
) : FurnitureWalkToUseLogic(stuffDataFactory, ctx), IFurniContext
{
    // Steps shown (ShowStepAsync) and when their mv comes off again, by avatar: one avatar tick
    // for the update carrying it to go out, and one more so the order of the room's turn cannot
    // matter.
    private readonly Dictionary<RoomObjectId, long> _shownSteps = [];

    private int _objectExtra;

    public FurniLogic Logic => logic;

    public RoomId Room => _ctx.RoomId;

    public RoomFurni Self => PluginRoomViews.Furni(_ctx.RoomObject);

    public IReadOnlyList<RoomUser> RoomUsers =>
        [.. AvatarModule.Avatars.Select(PluginRoomViews.User)];

    // As long as core's own walk-to furni wait for someone walking over (a switch).
    protected override int WalkTimeoutMs => RoomConfig.FloorSwitchWalkTimeoutMs;

    public Task SetStateAsync(int state, CancellationToken ct) => SetStateAsync(state);

    public Task FireWiredAsync<TEvent>(TEvent evt, RoomUser? triggerer, CancellationToken ct)
        where TEvent : IWiredEvent =>
        _ctx.PublishRoomEventAsync(PluginWiredEvents.Create(Room, evt, triggerer), ct);

    public T? ReadExtraData<T>(string section)
        where T : class =>
        FurnitureExtraDataSections.Read<T>(
            _ctx.RoomObject.ExtraData,
            _ctx.Definition.ExtraData,
            section,
            Logger
        );

    public int PairedItemId => ReadExtraData<RoomLinkerData>(RoomLinkerData.SECTION)?.ItemId ?? 0;

    public Task<RoomId?> GetRoomOfItemAsync(int itemId, CancellationToken ct) =>
        itemId > 0 ? FurniModule.GetRoomIdOfItemAsync(itemId, ct) : Task.FromResult<RoomId?>(null);

    public int ObjectExtra => _objectExtra;

    public override int GetObjectExtra() => _objectExtra;

    public Task SetObjectExtraAsync(int extra, CancellationToken ct)
    {
        if (_objectExtra == extra)
            return Task.CompletedTask;

        _objectExtra = extra;
        _ctx.RoomObject.MarkDirty();

        return _ctx.UpdateItemAsync();
    }

    public Task<bool> WalkToAsync(
        RoomUser user,
        IReadOnlyList<RoomTilePoint> tiles,
        Func<RoomUser, CancellationToken, Task> onArrived,
        CancellationToken ct
    )
    {
        if (!TryGetAvatar(user, out var avatar))
            return Task.FromResult(false);

        var map = MapModule;

        // Nearest first, as the furni's own walk-to-use tiles are ordered.
        var ordered = tiles
            .Where(tile => map.InBounds(tile.X, tile.Y))
            .Distinct()
            .OrderBy(tile =>
                (tile.X - avatar.X) * (tile.X - avatar.X)
                + (tile.Y - avatar.Y) * (tile.Y - avatar.Y)
            )
            .Select(tile => map.ToIdx(tile.X, tile.Y));

        return WalkToAsync(
            avatar,
            ordered,
            (arrived, token) => onArrived(PluginRoomViews.User(arrived), token),
            ct
        );
    }

    public async Task<bool> StopWalkingAsync(RoomUser user, CancellationToken ct)
    {
        if (!TryGetAvatar(user, out var avatar))
            return false;

        await AvatarModule.StopWalkingAsync(avatar, ct);

        return true;
    }

    public Task<bool> SetRotationAsync(RoomUser user, Rotation rotation, CancellationToken ct)
    {
        if (!TryGetAvatar(user, out var avatar))
            return Task.FromResult(false);

        avatar.SetRotation(rotation);
        avatar.MarkDirty();

        return Task.FromResult(true);
    }

    public Task<bool> ShowStepAsync(RoomUser user, RoomTilePoint to, CancellationToken ct)
    {
        if (!TryGetAvatar(user, out var avatar))
            return Task.FromResult(false);

        var map = MapModule;
        var height = map.InBounds(to.X, to.Y)
            ? map.GetTileHeightForAvatar(map.ToIdx(to.X, to.Y))
            : avatar.Z;

        avatar.AddStatus(AvatarStatusType.Move, $"{to.X},{to.Y},{height.ToStatusString()}");
        avatar.SetRotation(RotationExtensions.FromPoints(avatar.X, avatar.Y, to.X, to.Y));
        avatar.MarkDirty();

        _shownSteps[avatar.ObjectId] = NowMs() + (2L * RoomConfig.AvatarTickMs);
        ScheduleTick();

        return Task.FromResult(true);
    }

    public async Task<bool> ForwardToRoomAsync(
        RoomUser user,
        RoomId room,
        RoomEntryExtension? extension,
        CancellationToken ct
    )
    {
        if (!TryGetAvatar(user, out var avatar) || avatar is not IRoomPlayer player || room <= 0)
            return false;

        // A linked pair's half sending someone to the room its other half stands in passes that
        // room's door as a teleporter does: both halves were placed by someone with rights in both
        // rooms. Checked here, never taken from the plugin. Any other forward is a plain entry.
        var throughPair =
            linkedPair
            && PairedItemId is > 0 and var partnerId
            && await FurniModule.GetRoomIdOfItemAsync(partnerId, ct) == room;

        // Told, not awaited: this runs in the room turn. Never a teleport's entry: the other
        // room's arrival policies place them, and its bans, capacity and entry policies decide.
        GrainFactory
            .ForwardPlayerToRoomAsync(
                player.PlayerId,
                room,
                new RoomEntrySnapshot
                {
                    Method = RoomEntryMethodType.Default,
                    TeleportId = 0,
                    SourceRoomId = Room,
                    Extension = extension,
                    ThroughLinkedPair = throughPair,
                },
                CancellationToken.None
            )
            .LogAndForget(
                Logger,
                "forward player {PlayerId} to room {RoomId} for a plugin furni",
                player.PlayerId,
                room
            );

        return true;
    }

    /// <summary>
    /// Hands the plugin a message for this furni; the reply says whether it took it. A logic that
    /// throws is logged and answered <see cref="FurniLogicReply.Failed"/>.
    /// </summary>
    public async Task<FurniLogicReply> ReceiveMessageAsync(
        FurniLogicMessage message,
        CancellationToken ct
    )
    {
        var sender =
            message.Sender is { } playerId && AvatarModule.TryGetPlayer(playerId, out var player)
                ? PluginRoomViews.User(player)
                : null;

        try
        {
            return await logic.OnMessageAsync(this, message, sender, ct)
                ?? FurniLogicReply.NotHandled;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError(
                ex,
                "The plugin furni logic {Logic} of item {ItemId} in room {RoomId} failed on a message",
                logic.GetType().Name,
                _ctx.ObjectId.Value,
                Room.Value
            );

            return FurniLogicReply.Failed;
        }
    }

    public override async Task OnAttachAsync(CancellationToken ct)
    {
        await base.OnAttachAsync(ct);
        await logic.OnAttachedAsync(this, ct);
    }

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        var user = AvatarModule.TryGetPlayer(ctx.PlayerId, out var player)
            ? PluginRoomViews.User(player)
            : null;

        if (!await logic.OnUseAsync(this, user, param, ct))
            await base.OnUseAsync(ctx, param, ct);
    }

    public override async Task OnWalkOnAsync(IRoomAvatarContext ctx, CancellationToken ct)
    {
        await base.OnWalkOnAsync(ctx, ct);
        await logic.OnWalkOnAsync(this, PluginRoomViews.User(ctx.RoomObject), ct);
    }

    public override async Task OnWalkOffAsync(IRoomAvatarContext ctx, CancellationToken ct)
    {
        await base.OnWalkOffAsync(ctx, ct);
        await logic.OnWalkOffAsync(this, PluginRoomViews.User(ctx.RoomObject), ct);
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        _shownSteps.Clear();

        return base.OnPickupAsync(ctx, ct);
    }

    // A plugin's walks always name their own arrival (WalkToAsync above).
    protected override Task UseFromTileAsync(
        ActionContext ctx,
        IRoomAvatar avatar,
        CancellationToken ct
    ) => Task.CompletedTask;

    /// <summary>
    /// Takes a shown step's mv off once the update carrying it has gone out, quietly: the user
    /// stays where it stands, and its next update says so. A user who started walking since keeps
    /// the mv of its own step.
    /// </summary>
    protected override Task<bool> TickMoreAsync(long nowMs, CancellationToken ct)
    {
        foreach (var (objectId, dueAtMs) in _shownSteps.ToArray())
        {
            if (nowMs < dueAtMs)
                continue;

            _shownSteps.Remove(objectId);

            if (AvatarModule.TryGetAvatar(objectId, out var avatar) && !avatar.IsWalking)
                avatar.RemoveStatus(AvatarStatusType.Move);
        }

        return Task.FromResult(_shownSteps.Count > 0);
    }

    private bool TryGetAvatar(RoomUser user, [NotNullWhen(true)] out IRoomAvatar? avatar) =>
        AvatarModule.TryGetAvatar(new RoomObjectId(user.Index), out avatar);
}

/// <summary>A plugin's wired event as the room carries it (<see cref="PluginWiredEvent"/>).</summary>
public static class PluginWiredEvents
{
    public static PluginWiredEvent Create<TEvent>(RoomId room, TEvent evt, RoomUser? triggerer)
        where TEvent : IWiredEvent =>
        new()
        {
            RoomId = room,
            CausedBy = triggerer?.Player is { } player
                ? ActionContext.CreateForPlayer(player, room)
                : ActionContext.CreateForSystem(room),
            EventKey = WiredEvents.KeyOf(typeof(TEvent)),
            Payload = JsonSerializer.Serialize(evt, typeof(TEvent)),
            TriggererId = triggerer is { Player: null } other
                ? new RoomObjectId(other.Index)
                : null,
        };

    /// <summary>The event fired by the hotel API, its triggerer named by player id.</summary>
    public static PluginWiredEvent FromHotel<TEvent>(
        RoomId room,
        TEvent evt,
        Primitives.Players.PlayerId? triggerer
    )
        where TEvent : IWiredEvent =>
        new()
        {
            RoomId = room,
            CausedBy = triggerer is { } player
                ? ActionContext.CreateForPlayer(player, room)
                : ActionContext.CreateForSystem(room),
            EventKey = WiredEvents.KeyOf(typeof(TEvent)),
            Payload = JsonSerializer.Serialize(evt, typeof(TEvent)),
        };
}
