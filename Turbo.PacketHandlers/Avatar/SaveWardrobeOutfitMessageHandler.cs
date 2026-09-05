using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Avatar;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Avatar;

public class SaveWardrobeOutfitMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SaveWardrobeOutfitMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SaveWardrobeOutfitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || string.IsNullOrWhiteSpace(message.Figure))
            return;

        await _grainFactory
            .GetPlayerWardrobeGrain(ctx.PlayerId)
            .SaveOutfitAsync(message.SlotId, message.Figure, message.Gender, ct)
            .ConfigureAwait(false);
    }
}
