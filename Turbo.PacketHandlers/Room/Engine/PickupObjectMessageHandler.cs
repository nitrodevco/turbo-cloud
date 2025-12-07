using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Engine;

public class PickupObjectMessageHandler(IRoomService roomService)
    : IMessageHandler<PickupObjectMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        PickupObjectMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var categoryId = message.CategoryId;

        if (categoryId == 1)
        {
            // wall
            return;
        }

        if (categoryId == 2)
        {
            await _roomService
                .PickupFloorItemInRoomAsync(
                    ctx.AsActionContext(),
                    message.ObjectId,
                    ct,
                    message.Confirm
                )
                .ConfigureAwait(false);
        }
    }
}
