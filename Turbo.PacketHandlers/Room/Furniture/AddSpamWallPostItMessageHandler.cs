using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Pins a note to a post-it wall.
/// </summary>
public class AddSpamWallPostItMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AddSpamWallPostItMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AddSpamWallPostItMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new AddSpamWallPostItInteraction
                {
                    Location = message.Location,
                    Color = message.Color,
                    Text = message.Text,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
