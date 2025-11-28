using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Session;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Session;

public class QuitMessageHandler(ISessionGateway sessionGateway, IRoomService roomService)
    : IMessageHandler<QuitMessage>
{
    private readonly ISessionGateway _sessionGateway = sessionGateway;
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        QuitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _roomService.CloseRoomForPlayerAsync(ctx.PlayerId).ConfigureAwait(false);
    }
}
