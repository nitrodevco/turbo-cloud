using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Engine;

public class GetHeightMapMessageHandler(IRoomService roomService)
    : IMessageHandler<GetHeightMapMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        GetHeightMapMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _roomService.EnterPendingRoomForPlayerIdAsync(ctx.PlayerId, ct).ConfigureAwait(false);
    }
}
