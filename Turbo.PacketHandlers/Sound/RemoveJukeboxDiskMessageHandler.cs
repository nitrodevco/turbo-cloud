using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Sound;

namespace Turbo.PacketHandlers.Sound;

/// <summary>The jukebox's owner takes a disk out of its playlist; it goes back to whoever put it in, and the room is sent the new playlist.</summary>
public class RemoveJukeboxDiskMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RemoveJukeboxDiskMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RemoveJukeboxDiskMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithMusicPlayerAsync(
                _grainFactory,
                new RemoveJukeboxDiskInteraction { Slot = message.Slot },
                ct
            )
            .ConfigureAwait(false);
    }
}
