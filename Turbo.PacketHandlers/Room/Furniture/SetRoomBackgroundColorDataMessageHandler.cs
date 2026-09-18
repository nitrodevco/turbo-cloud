using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Sets the room background toner colour.
/// </summary>
public class SetRoomBackgroundColorDataMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetRoomBackgroundColorDataMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetRoomBackgroundColorDataMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ObjectId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .InteractWithItemAsync(
                ctx.AsActionContext(),
                message.ObjectId,
                new SetBackgroundTonerInteraction
                {
                    Hue = message.Hue,
                    Saturation = message.Saturation,
                    Lightness = message.Lightness,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
