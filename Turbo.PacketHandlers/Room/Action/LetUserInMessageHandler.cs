using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Room.Action;

public class LetUserInMessageHandler(IRoomService roomService) : IMessageHandler<LetUserInMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        LetUserInMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || string.IsNullOrWhiteSpace(message.Name))
            return;

        await _roomService
            .AnswerDoorbellAsync(ctx.AsActionContext(), message.Name, message.CanEnter, ct)
            .ConfigureAwait(false);
    }
}
