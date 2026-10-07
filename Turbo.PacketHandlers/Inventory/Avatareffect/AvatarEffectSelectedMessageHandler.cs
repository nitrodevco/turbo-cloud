using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Avatareffect;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Avatareffect;

/// <summary>Wears an effect the player has running, or takes the worn one off.</summary>
public class AvatarEffectSelectedMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AvatarEffectSelectedMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AvatarEffectSelectedMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerEffectGrain(ctx.PlayerId)
            .SelectEffectAsync(message.Type, ct)
            .ConfigureAwait(false);
    }
}
