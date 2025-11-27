using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Extensions;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Engine;

public class MoveObjectMessageHandler(IRoomService roomService) : IMessageHandler<MoveObjectMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        MoveObjectMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _roomService
            .MoveFloorItemInRoomAsync(
                ctx.ToActionContext(),
                message.ObjectId,
                message.X,
                message.Y,
                message.Rotation,
                ct
            )
            .ConfigureAwait(false);
    }
}
