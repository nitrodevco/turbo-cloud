using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

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

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _delayMs = Math.Clamp(_wiredData.GetDefinitionParam<int>(0), 0, 20) * WiredPulses.MS;
    }
}
