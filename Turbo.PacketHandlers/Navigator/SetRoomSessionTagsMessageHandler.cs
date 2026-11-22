using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Navigator;

/// <summary>The owner names the room's tags from its info; the room holds them to its limits.</summary>
public class SetRoomSessionTagsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetRoomSessionTagsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetRoomSessionTagsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .SetTagsAsync(
                ctx.AsActionContext(),
                [message.Tag1 ?? string.Empty, message.Tag2 ?? string.Empty],
                ct
            )
            .ConfigureAwait(false);
    }
}
