using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Chat;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Chat;

public class ChatMessageHandler(IGrainFactory grainFactory) : IMessageHandler<ChatMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ChatMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var roomChatGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        await roomChatGrain
            .SendChatFromPlayerAsync(
                ctx.PlayerId,
                message.Text,
                0,
                message.StyleId,
                [],
                message.TrackingId
            )
            .ConfigureAwait(false);
    }
}
