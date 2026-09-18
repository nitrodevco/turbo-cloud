using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Engine;

/// <summary>
/// Saves a post-it note. The grain validates the colour and text length and broadcasts the update.
/// </summary>
public class SetItemDataMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetItemDataMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetItemDataMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ItemId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .SetItemDataAsync(
                ctx.AsActionContext(),
                message.ItemId,
                message.Color,
                message.Text,
                ct
            )
            .ConfigureAwait(false);
    }
}
