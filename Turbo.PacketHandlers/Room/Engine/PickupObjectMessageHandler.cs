using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Engine;

public class PickupObjectMessageHandler(IRoomService roomService)
    : IMessageHandler<PickupObjectMessage>
{
    /// <summary>
    /// The categories the Flash client sends (<c>PickupObjectMessageComposer.getMessageArray</c>):
    /// a floor item (room object category 10) as 2, a wall item (20) as 1.
    /// </summary>
    public const int CATEGORY_FLOOR = 2;
    public const int CATEGORY_WALL = 1;

    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        PickupObjectMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        switch (message.CategoryId)
        {
            case CATEGORY_FLOOR:
            case CATEGORY_WALL:
                await _roomService
                    .PickupItemInRoomAsync(
                        ctx.AsActionContext(),
                        message.ObjectId,
                        ct,
                        message.Confirm
                    )
                    .ConfigureAwait(false);

                return;
        }
    }
}
