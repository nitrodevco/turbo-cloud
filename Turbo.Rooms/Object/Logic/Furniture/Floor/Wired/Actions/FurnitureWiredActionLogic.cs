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

    private long _delayMs = 0;

    public override List<Type> GetDefinitionSpecificTypes() =>
        [.. base.GetDefinitionSpecificTypes(), typeof(int)];

    public long GetDelayMs() => _delayMs;

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
            .LogAndForget(
                _roomGrain._logger,
                "publish an event in room {RoomId}",
                _roomGrain.RoomId
            );

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _delayMs = WiredPulses.ActionDelayToMs(
            _wiredData.GetDefinitionParam<int>(0),
            _roomGrain._wiredConfig.ActionDelayMaxPulses
        );
    }
}
