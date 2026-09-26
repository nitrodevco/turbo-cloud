using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>A video display was opened: its playlists and what it is playing.</summary>
public class GetYoutubeDisplayStatusMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetYoutubeDisplayStatusMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetYoutubeDisplayStatusMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new RequestYoutubeStatusInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
