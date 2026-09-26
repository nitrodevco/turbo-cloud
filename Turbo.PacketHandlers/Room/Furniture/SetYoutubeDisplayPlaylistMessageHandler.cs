using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>Switches a video display to another of the hotel playlists.</summary>
public class SetYoutubeDisplayPlaylistMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetYoutubeDisplayPlaylistMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetYoutubeDisplayPlaylistMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (string.IsNullOrEmpty(message.PlaylistId))
            return;

        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new SetYoutubePlaylistInteraction { PlaylistId = message.PlaylistId },
                ct
            )
            .ConfigureAwait(false);
    }
}
