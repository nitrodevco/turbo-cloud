using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

internal static class CatalogPurchaseExtensions
{
    /// <summary>
    /// Buys an offer and tells the client how it went. Every packet that spends money on a
    /// catalog offer ends here, so a refusal reads the same whether it came from the shop front
    /// or from a membership button.
    /// </summary>
    public static async Task PurchaseOfferAsync(
        this MessageContext ctx,
        IGrainFactory grainFactory,
        CatalogType catalogType,
        int offerId,
        string extraParam,
        int quantity,
        CancellationToken ct
    )
    {
        try
        {
            var offer = await grainFactory
                .GetCatalogPurchaseGrain(ctx.PlayerId)
                .PurchaseOfferFromCatalogAsync(catalogType, offerId, extraParam, quantity, ct)
                .ConfigureAwait(false);

            await ctx.SendComposerAsync(new PurchaseOKMessageComposer { Offer = offer }, ct)
                .ConfigureAwait(false);
        }
        catch (CatalogPurchaseException ex)
        {
            await ctx.SendPurchaseFailureAsync(ex, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Turns a refused purchase into the message the client draws for it: a balance failure has
    /// its own packet, and the rest split by whether the client shows an error or a refusal.
    /// </summary>
    public static async Task SendPurchaseFailureAsync(
        this MessageContext ctx,
        CatalogPurchaseException ex,
        CancellationToken ct
    )
    {
        if (ex.BalanceFailure is not null)
        {
            await ctx.SendComposerAsync(
                    new NotEnoughBalanceMessageComposer
                    {
                        NotEnoughCredits = ex.BalanceFailure.NotEnoughCredits,
                        NotEnoughActivityPoints = ex.BalanceFailure.NotEnoughActivityPoints,
                        ActivityPointType = ex.BalanceFailure.ActivityPointType,
                    },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        if ((int)ex.ErrorType < 100)
        {
            await ctx.SendComposerAsync(
                    new PurchaseErrorMessageComposer { ErrorCode = ex.ErrorType },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        var errorCode =
            ex.ErrorType == CatalogPurchaseErrorType.RequiresHabboClub ? 1 : (int)ex.ErrorType;

        await ctx.SendComposerAsync(
                new PurchaseNotAllowedMessageComposer
                {
                    ErrorType = (CatalogPurchaseErrorType)errorCode,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
