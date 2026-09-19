using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Orleans;

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
        if (
            ctx.PlayerId <= 0
            || ctx.RoomId <= 0
            || message.ObjectId <= 0
            || string.IsNullOrEmpty(message.PlaylistId)
        )
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .InteractWithItemAsync(
                ctx.AsActionContext(),
                message.ObjectId,
                new SetYoutubePlaylistInteraction { PlaylistId = message.PlaylistId },
                ct
            )
            .ConfigureAwait(false);
    }
}
