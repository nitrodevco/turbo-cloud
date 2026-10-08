using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Sound;

namespace Turbo.PacketHandlers.Sound;

/// <summary>Sent when a jukebox appears: answered with what it plays and how far in, so the client joins the song where the room is.</summary>
public class GetNowPlayingMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetNowPlayingMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetNowPlayingMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithMusicPlayerAsync(
                _grainFactory,
                new RequestNowPlayingInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
