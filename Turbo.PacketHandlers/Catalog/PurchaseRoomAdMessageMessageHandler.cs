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
            await ctx.SendPurchaseErrorAsync(CatalogPurchaseErrorType.PurchaseFailed, ct)
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
            await ctx.SendPurchaseFailureAsync(ex, ct).ConfigureAwait(false);
        }
    }
}
