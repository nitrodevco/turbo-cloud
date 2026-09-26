using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

public abstract class FurnitureWiredActionLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredLogic(grainFactory, stuffDataFactory, ctx), IWiredAction
{
    public override WiredType WiredType => WiredType.Action;

    private int _delayMs = 0;

    public override List<Type> GetDefinitionSpecificTypes() =>
        [.. base.GetDefinitionSpecificTypes(), typeof(int)];

    public int GetDelayMs() => _delayMs;

    public virtual bool IsNegative => false;

    public virtual Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct) =>
        Task.FromResult(true);

    /// <summary>
    /// Moves the furni in slot 0 onto the first furni in slot 1, offset by
    /// (<paramref name="dx"/>, <paramref name="dy"/>) tiles. "Furni to furni" is this with no
    /// offset and "move furni to" with one; they were the same loop written twice.
    /// </summary>
    protected async Task<bool> MoveOntoTargetFurniAsync(IWiredExecutionContext ctx, int dx, int dy)
    {
        var movers = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 0));
        var targets = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 1));

        if (movers.Count == 0 || targets.Count == 0)
            return false;

        var target = targets[0];
        var moved = false;

        foreach (var mover in movers)
        {
            if (mover.ObjectId == target.ObjectId)
                continue;

            moved |= await ctx.TryMoveFloorItemAsync(mover, target.X + dx, target.Y + dy);
        }

        return moved;
    }

    /// <summary>
    /// A furni this action moves ran into a player: what the "furni collides with user" trigger
    /// listens for. Queued, so the action does not wait for whatever the trigger sets off.
    /// </summary>
    protected void PublishCollision(IRoomFloorItem item, IRoomPlayer player) =>
        _ctx.PublishRoomEventAsync(
                new RoomItemCollisionEvent
                {
                    RoomId = _roomGrain.RoomId,
                    CausedBy = ActionContext.CreateForPlayer(player.PlayerId, _roomGrain.RoomId),
                    ObjectId = item.ObjectId,
                },
                CancellationToken.None
            )
            .LogAndForget(_roomGrain._logger, $"publish an event in room {_roomGrain.RoomId}");

    /// <summary>
    /// Asked first by a box that sets more wired off (a signal, a stack call): past
    /// <c>WiredConfig.MaxDepth</c> it records the error the wired log shows and says so. The
    /// receiving end checks the same limit on the event it gets.
    /// </summary>
    protected bool IsCallDepthExceeded(IWiredExecutionContext ctx)
    {
        if (ctx.Depth < _roomGrain._wiredConfig.MaxDepth)
            return false;

        _roomGrain.WiredSystem.RecordError(
            "WiredCallDepthExceeded",
            Grains.Systems.RoomWiredSystem.GetErrorCategory(this),
            _roomGrain.NowMs()
        );

        return true;
    }

    /// <summary>
    /// Puts an avatar on a tile at once, the way every wired teleport does: a freeze that ends on
    /// a teleport ends here, then the move joins the stack's movement packet. The teleport box
    /// and the "user to furni" box in teleport mode both come here; the second used to skip the
    /// thaw.
    /// </summary>
    protected static Task<bool> TeleportAvatarAsync(
        IWiredExecutionContext ctx,
        IRoomAvatar avatar,
        int tileIdx
    )
    {
        if (avatar.IsFrozen && avatar.ThawsOnTeleport)
            avatar.SetFrozen(false);

        return ctx.ProcessUserMovementAsync(avatar, tileIdx, SlideAvatarMoveType.None);
    }

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _delayMs = Math.Clamp(_wiredData.GetDefinitionParam<int>(0), 0, 20) * WiredPulses.MS;
    }
}
