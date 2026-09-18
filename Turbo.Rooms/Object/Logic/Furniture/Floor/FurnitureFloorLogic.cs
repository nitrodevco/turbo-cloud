using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

[RoomObjectLogic("default_floor")]
public class FurnitureFloorLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureLogic<IRoomFloorItem, IFurnitureFloorLogic, IRoomFloorItemContext>(
        stuffDataFactory,
        ctx
    ),
        IFurnitureFloorLogic
{
    IRoomFloorItemContext IFurnitureFloorLogic.Context => Context;

    public virtual bool CanStack() => _ctx.Definition.CanStack;

    public virtual bool CanWalk() => _ctx.Definition.CanWalk;

    public virtual bool CanSit() => _ctx.Definition.CanSit;

    public virtual bool CanLay() => _ctx.Definition.CanLay;

    public override bool CanRoll() => true;

    public virtual Altitude GetPostureOffset()
    {
        if (CanSit())
            return GetStackHeight();

        if (CanLay())
            return GetStackHeight();

        return Altitude.Zero;
    }

    public override Altitude GetStackHeight() => _ctx.Definition.StackHeight;

    public override Task OnStateChangedAsync(CancellationToken ct)
    {
        _ctx.RefreshTile();

        return base.OnStateChangedAsync(ct);
    }

    public virtual Task OnInvokeAsync(IRoomAvatarContext ctx, CancellationToken ct) =>
        Task.CompletedTask;

    /// <summary>The acting player's avatar, or null when they are not in the room.</summary>
    protected IRoomAvatar? GetAvatar(ActionContext ctx) =>
        _roomGrain._state.AvatarsByPlayerId.TryGetValue(ctx.PlayerId, out var objectId)
        && _roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var avatar)
            ? avatar
            : null;

    /// <summary>
    /// Whether the acting player's avatar stands on or next to this item. The client only offers
    /// dice, wheel and similar actions from that distance, so the server holds the same line.
    /// </summary>
    protected bool IsAvatarAdjacent(ActionContext ctx)
    {
        var avatar = GetAvatar(ctx);

        if (avatar is null)
            return false;

        var item = _ctx.RoomObject;
        var dx = Math.Max(
            item.X - avatar.X,
            Math.Max(0, avatar.X - (item.X + _ctx.Definition.Width - 1))
        );
        var dy = Math.Max(
            item.Y - avatar.Y,
            Math.Max(0, avatar.Y - (item.Y + _ctx.Definition.Length - 1))
        );

        return Math.Max(dx, dy) <= 1;
    }

    public virtual Task OnWalkOnAsync(IRoomAvatarContext ctx, CancellationToken ct) =>
        _ctx.PublishRoomEventAsync(
            new AvatarWalkOnFurniEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ActionContext.CreateForObjectContext(ctx),
                ObjectId = ctx.ObjectId,
                FurniId = _ctx.ObjectId,
            },
            ct
        );

    public virtual Task OnWalkOffAsync(IRoomAvatarContext ctx, CancellationToken ct) =>
        _ctx.PublishRoomEventAsync(
            new AvatarWalkOffFurniEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ActionContext.CreateForObjectContext(ctx),
                ObjectId = ctx.ObjectId,
                FurniId = _ctx.ObjectId,
            },
            ct
        );
}
