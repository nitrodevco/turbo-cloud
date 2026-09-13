using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Chat;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Chat;

public class CancelTypingMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<CancelTypingMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        CancelTypingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .SetAvatarTypingAsync(ctx.AsActionContext(), false, ct)
            .ConfigureAwait(false);
    }
}
