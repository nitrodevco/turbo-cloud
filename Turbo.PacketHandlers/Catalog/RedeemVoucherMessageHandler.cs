using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class RedeemVoucherMessageHandler(IVoucherService vouchers)
    : IMessageHandler<RedeemVoucherMessage>
{
    public async ValueTask HandleAsync(
        RedeemVoucherMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var result = await vouchers
            .RedeemAsync(ctx.PlayerId, message.Code ?? string.Empty, ct)
            .ConfigureAwait(false);

        if (result.Error is { } error)
        {
            await ctx.SendComposerAsync(new VoucherRedeemErrorMessageComposer { Error = error }, ct)
                .ConfigureAwait(false);

            return;
        }

        await ctx.SendComposerAsync(
                new VoucherRedeemOkMessageComposer
                {
                    ProductName = result.ProductName,
                    ProductDescription = result.ProductDescription,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
