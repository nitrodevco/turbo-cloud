using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Sound;

namespace Turbo.PacketHandlers.Sound;

/// <summary>Sent when a sound machine is switched on with no songs known: answered with its songs and how far into them the room is.</summary>
public class GetSoundMachinePlayListMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetSoundMachinePlayListMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetSoundMachinePlayListMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithMusicPlayerAsync(
                _grainFactory,
                new RequestSoundMachinePlaylistInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
