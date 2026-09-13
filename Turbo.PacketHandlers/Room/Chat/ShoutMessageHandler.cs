using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Chat;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.PacketHandlers.Room.Chat;

public class ShoutMessageHandler(IGrainFactory grainFactory) : IMessageHandler<ShoutMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ShoutMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || string.IsNullOrWhiteSpace(message.Text))
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .SendChatFromPlayerAsync(
                ctx.AsActionContext(),
                RoomChatType.Shout,
                message.Text,
                message.StyleId,
                ct
            )
            .ConfigureAwait(false);
    }
}
