using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>The player closed their trade with wired.</summary>
public class WiredTradeCancelMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredTradeCancelMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredTradeCancelMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetWiredTradeGrain(ctx.PlayerId)
            .CancelAsync(WiredTransactionFailureType.UserCancelled, ct)
            .ConfigureAwait(false);
    }
}
