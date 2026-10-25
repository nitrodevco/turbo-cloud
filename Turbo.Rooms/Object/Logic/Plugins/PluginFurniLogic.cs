using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Furni;
using Turbo.Contracts.Rooms;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Object.Logic.Plugins;

/// <summary>
/// A plugin's <see cref="FurniLogic"/> run as a floor furni logic of core's: each hook runs after
/// core's own (a walk-on still reaches wired), and a use the plugin does not handle does what the
/// furni does by default.
/// </summary>
internal sealed class PluginFurniLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx,
    FurniLogic logic
) : FurnitureFloorLogic(stuffDataFactory, ctx), IFurniContext
{
    public FurniLogic Logic => logic;

    public RoomId Room => _ctx.RoomId;

    public RoomFurni Self => PluginRoomViews.Furni(_ctx.RoomObject);

    public IReadOnlyList<RoomUser> RoomUsers =>
        [.. AvatarModule.Avatars.Select(PluginRoomViews.User)];

    public Task SetStateAsync(int state, CancellationToken ct) => SetStateAsync(state);

    public Task FireWiredAsync<TEvent>(TEvent evt, RoomUser? triggerer, CancellationToken ct)
        where TEvent : IWiredEvent =>
        _ctx.PublishRoomEventAsync(PluginWiredEvents.Create(Room, evt, triggerer), ct);

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
