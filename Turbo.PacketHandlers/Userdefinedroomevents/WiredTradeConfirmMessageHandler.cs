using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>Accepts the player's trade with wired, or confirms it after the countdown.</summary>
public class WiredTradeConfirmMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredTradeConfirmMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredTradeConfirmMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetWiredTradeGrain(ctx.PlayerId)
            .ConfirmAsync(message.IsFinalConfirm, ct)
            .ConfigureAwait(false);
    }
}
