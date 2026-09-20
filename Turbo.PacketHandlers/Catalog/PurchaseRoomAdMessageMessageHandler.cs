using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

public class PurchaseRoomAdMessageMessageHandler(
    INavigatorService navigatorService,
    IGrainFactory grainFactory
) : IMessageHandler<PurchaseRoomAdMessageMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        PurchaseRoomAdMessageMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.FlatId <= 0)
            return;

        var name = message.Name?.Trim() ?? string.Empty;
        var description = message.Description?.Trim() ?? string.Empty;

        if (
            !_navigatorService
                .GetEventCategories()
                .Any(x => x.Id == message.CategoryId && x.Visible)
        )
        {
            await SendPurchaseErrorAsync(ctx, CatalogPurchaseErrorType.PurchaseFailed, ct)
                .ConfigureAwait(false);

            return;
        }

        try
        {
            var offer = await _grainFactory
                .GetCatalogPurchaseGrain(ctx.PlayerId)
                .PurchaseRoomAdAsync(
                    message.OfferId,
                    message.FlatId,
                    message.CategoryId,
                    name,
                    description,
                    message.Extended,
                    _navigatorService.RoomEventDuration,
                    ct
                )
                .ConfigureAwait(false);

            await ctx.SendComposerAsync(new PurchaseOKMessageComposer { Offer = offer }, ct)
                .ConfigureAwait(false);
        }
        catch (CatalogPurchaseException ex)
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

            await SendPurchaseErrorAsync(ctx, ex.ErrorType, ct).ConfigureAwait(false);
        }
    }

    private static Task SendPurchaseErrorAsync(
        MessageContext ctx,
        CatalogPurchaseErrorType errorType,
        CancellationToken ct
    ) =>
        (int)errorType < 100
            ? ctx.SendComposerAsync(new PurchaseErrorMessageComposer { ErrorCode = errorType }, ct)
            : ctx.SendComposerAsync(
                new PurchaseNotAllowedMessageComposer { ErrorType = errorType },
                ct
            );
}
