using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
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
    /// its own packet, and the rest go by error type.
    /// </summary>
    public static Task SendPurchaseFailureAsync(
        this MessageContext ctx,
        CatalogPurchaseException ex,
        CancellationToken ct
    ) =>
        ex.BalanceFailure is { } balanceFailure
            ? ctx.SendBalanceFailureAsync(balanceFailure, ct)
            : ctx.SendPurchaseErrorAsync(ex.ErrorType, ct);

    /// <summary>
    /// Tells the client which currency the buyer was short of.
    /// </summary>
    public static Task SendBalanceFailureAsync(
        this MessageContext ctx,
        CatalogBalanceFailure balanceFailure,
        CancellationToken ct
    ) =>
        ctx.SendComposerAsync(
            new NotEnoughBalanceMessageComposer
            {
                NotEnoughCredits = balanceFailure.NotEnoughCredits,
                NotEnoughActivityPoints = balanceFailure.NotEnoughActivityPoints,
                ActivityPointType = balanceFailure.ActivityPointType,
            },
            ct
        );

    /// <summary>
    /// Sends a purchase error on whichever packet can word it. Codes below 100 are the client's
    /// described purchase errors; the rest are refusals, where the client words only code 1 (club
    /// required) and shows every other code as an unknown refusal.
    /// </summary>
    public static Task SendPurchaseErrorAsync(
        this MessageContext ctx,
        CatalogPurchaseErrorType errorType,
        CancellationToken ct
    )
    {
        if ((int)errorType < 100)
            return ctx.SendComposerAsync(
                new PurchaseErrorMessageComposer { ErrorCode = errorType },
                ct
            );

        var errorCode =
            errorType == CatalogPurchaseErrorType.RequiresHabboClub ? 1 : (int)errorType;

        return ctx.SendComposerAsync(
            new PurchaseNotAllowedMessageComposer
            {
                ErrorType = (CatalogPurchaseErrorType)errorCode,
            },
            ct
        );
    }
}
