using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Sound;

namespace Turbo.PacketHandlers.Sound;

/// <summary>
/// The jukebox's owner puts a song disk from their inventory into its playlist. A full playlist
/// is answered with <c>JukeboxPlayListFull</c>; otherwise the room is sent the new playlist.
/// </summary>
public class AddJukeboxDiskMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AddJukeboxDiskMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AddJukeboxDiskMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (message.DiskId <= 0)
            return;

        await ctx.InteractWithMusicPlayerAsync(
                _grainFactory,
                new AddJukeboxDiskInteraction { DiskId = message.DiskId, Slot = message.Slot },
                ct
            )
            .ConfigureAwait(false);
    }
}
