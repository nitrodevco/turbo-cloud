using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

[RoomObjectLogic("roller")]
public class FurnitureRollerLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public override bool CanRoll() => false;

    public override async Task OnAttachAsync(CancellationToken ct)
    {
        await base.OnAttachAsync(ct);

        await _ctx.PublishRoomEventAsync(
            new RoomRollerChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ActionContext.CreateForSystem(_ctx.RoomId),
                ObjectId = _ctx.ObjectId,
            },
            ct
        );
    }

    public override async Task OnDetachAsync(CancellationToken ct)
    {
        await base.OnDetachAsync(ct);

        await _ctx.PublishRoomEventAsync(
            new RoomRollerChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ActionContext.CreateForSystem(_ctx.RoomId),
                ObjectId = _ctx.ObjectId,
            },
            ct
        );
    }

    public override async Task OnMoveAsync(ActionContext ctx, int prevIdx, CancellationToken ct)
    {
        await base.OnMoveAsync(ctx, prevIdx, ct);

        await _ctx.PublishRoomEventAsync(
            new RoomRollerChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ctx,
                ObjectId = _ctx.ObjectId,
            },
            ct
        );
    }
}
