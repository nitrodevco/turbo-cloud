using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Sound;

namespace Turbo.PacketHandlers.Sound;

/// <summary>Sent when a jukebox appears and when its playlist editor opens: answered with the room jukebox's disks, in playing order.</summary>
public class GetJukeboxPlayListMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetJukeboxPlayListMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetJukeboxPlayListMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithMusicPlayerAsync(
                _grainFactory,
                new RequestJukeboxPlaylistInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
