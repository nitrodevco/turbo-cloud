using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Action;

/// <summary>
/// Kicks a player out of the room; the room decides whether the actor may.
/// </summary>
public class KickUserMessageHandler(IRoomService roomService) : IMessageHandler<KickUserMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        KickUserMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.UserId <= 0)
            return;

        await _roomService
            .KickPlayerAsync(ctx.AsActionContext(), message.UserId, ct)
            .ConfigureAwait(false);
    }
}
