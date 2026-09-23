using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Layout;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.PacketHandlers.Room.Layout;

public class UpdateFloorPropertiesMessageHandler(IRoomService roomService)
    : IMessageHandler<UpdateFloorPropertiesMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        UpdateFloorPropertiesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _roomService
            .SaveFloorPlanAsync(
                ctx.AsActionContext(),
                message.ModelData,
                message.HasProperties
                    ? new FloorPlanPropertiesSnapshot
                    {
                        DoorX = message.DoorX,
                        DoorY = message.DoorY,
                        DoorRotation = message.DoorRotation,
                        WallThickness = message.WallThickness,
                        FloorThickness = message.FloorThickness,
                        FixedWallsHeight = message.FixedWallsHeight,
                    }
                    : null,
                ct
            )
            .ConfigureAwait(false);
    }
}
