using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Collectibles;
using Turbo.Primitives.Messages.Outgoing.Collectibles;

namespace Turbo.PacketHandlers.Collectibles;

/// <summary>
/// The transfer tab's fee. Habbo answers 0 (official client capture, build WIN63-202610151745); Turbo keeps no wallets, so there is nothing to transfer either.
/// </summary>
public class GetNftTransferFeeMessageHandler : IMessageHandler<GetNftTransferFeeMessage>
{
    public async ValueTask HandleAsync(
        GetNftTransferFeeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(new NftTransferFeeMessageComposer { TransferFee = 0 }, ct)
            .ConfigureAwait(false);
    }
}
