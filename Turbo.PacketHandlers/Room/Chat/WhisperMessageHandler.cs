using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Chat;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.PacketHandlers.Room.Chat;

public class WhisperMessageHandler(IGrainFactory grainFactory) : IMessageHandler<WhisperMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WhisperMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || string.IsNullOrWhiteSpace(message.Text)
            || string.IsNullOrWhiteSpace(message.RecipientName)
        )
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .SendChatFromPlayerAsync(
                ctx.AsActionContext(),
                RoomChatType.Whisper,
                message.Text,
                message.StyleId,
                ct,
                recipientName: message.RecipientName
            )
            .ConfigureAwait(false);
    }
}
