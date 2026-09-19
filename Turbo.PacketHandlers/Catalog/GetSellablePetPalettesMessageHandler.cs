using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Providers;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// The catalog pet page asks which breeds of a pet offer are for sale. The client names the
/// offer by its localization id, which for pets is the product code <c>pet&lt;typeId&gt;</c>;
/// an offer named otherwise is resolved through its pet product.
/// </summary>
public class GetSellablePetPalettesMessageHandler(
    IGrainFactory grainFactory,
    ICatalogService catalogService,
    IPetBreedProvider petBreedProvider
) : IMessageHandler<GetSellablePetPalettesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly ICatalogService _catalogService = catalogService;
    private readonly IPetBreedProvider _petBreedProvider = petBreedProvider;

    public async ValueTask HandleAsync(
        GetSellablePetPalettesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || !TryResolveTypeId(message.ProductCode, out var typeId))
            return;

        await _grainFactory
            .GetPlayerPresenceGrain(ctx.PlayerId)
            .SendComposerAsync(
                new SellablePetPalettesMessageComposer
                {
                    ProductCode = message.ProductCode,
                    Palettes = [.. _petBreedProvider.GetPalettes(typeId).Where(x => x.Sellable)],
                },
                ct
            )
            .ConfigureAwait(false);
    }

    private bool TryResolveTypeId(string? productCode, out int typeId)
    {
        if (PetProductCodes.TryGetTypeId(productCode, out typeId))
            return true;

        var snapshot = _catalogService.GetCatalogSnapshot(CatalogType.Normal);
        var offer = snapshot.OffersById.Values.FirstOrDefault(x => x.LocalizationId == productCode);
        var product = offer?.Products.FirstOrDefault(x => x.ProductType == ProductType.Pet);

        if (product is null)
            return false;

        return PetProductCodes.TryGetTypeId(product.ClassName, out typeId)
            || int.TryParse(product.ExtraParam, out typeId);
    }
}
