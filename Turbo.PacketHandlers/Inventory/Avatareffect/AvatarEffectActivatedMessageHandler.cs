using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Avatareffect;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Avatareffect;

/// <summary>Starts one of the player's waiting copies of an effect and wears it.</summary>
public class AvatarEffectActivatedMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AvatarEffectActivatedMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AvatarEffectActivatedMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerEffectGrain(ctx.PlayerId)
            .ActivateEffectAsync(message.Type, ct)
            .ConfigureAwait(false);
    }
}
