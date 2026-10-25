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
    /// A furni this action moves ran into a user: what the "furni collides with user" trigger
    /// listens for. A bot is a user here and triggers it: blocked by the bot Frank it fired with
    /// Frank as the triggerer (official client capture 2026-10-24, wired matrix trigger.11.R2,
    /// 3/3). Queued, so the action does not wait for whatever the trigger sets off.
    /// </summary>
    protected void PublishCollision(IRoomFloorItem item, IRoomAvatar avatar) =>
        _ctx.PublishRoomEventAsync(
                new RoomItemCollisionEvent
                {
                    RoomId = _roomGrain.RoomId,
                    CausedBy = avatar is IRoomPlayer player
                        ? ActionContext.CreateForPlayer(player.PlayerId, _roomGrain.RoomId)
                        : ActionContext.CreateForBot(_roomGrain.RoomId),
                    ObjectId = item.ObjectId,
                    AvatarObjectId = avatar.ObjectId,
                },
                CancellationToken.None
            )
            .LogAndForget(Logger, "publish an event in room {RoomId}", _roomGrain.RoomId);

    protected override async Task FillInternalDataAsync(CancellationToken ct)
    {
        await base.FillInternalDataAsync(ct);

        _delayMs = WiredPulses.ActionDelayToMs(
            _wiredData.GetDefinitionParam<int>(0),
            WiredConfig.ActionDelayMaxPulses
        );
    }
}
