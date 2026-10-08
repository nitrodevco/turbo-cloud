using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Inventory.Grains;

internal sealed partial class InventoryGrain
{
    public async Task ReceivePresentAsync(PresentGrantRequest request, CancellationToken ct)
    {
        var content = FurniModule.GetDefinitionOrThrow(request.Product.FurniDefinitionId);
        var present = FurniModule.GetDefinitionOrThrow(request.PresentDefinitionId);
        var contentExtraData = await BuildCatalogExtraDataAsync(
            content,
            request.Product,
            request.ExtraParam,
            request.BuyerName,
            ct
        );

        await FurniModule.GrantPresentAsync(
            present,
            BuildPresentExtraData(request, content.Name),
            content,
            contentExtraData,
            ct
        );
    }

    public Task<FurnitureItemSnapshot?> UnwrapPresentAsync(
        RoomObjectId presentId,
        CancellationToken ct
    ) => FurniModule.UnwrapPresentAsync(presentId, ct);

    /// <summary>
    /// What the present carries: the tag the client's present logic reads from its map data
    /// (the note, and the buyer's name and face unless the gift is anonymous), the product code
    /// it shows once opened, and the box and ribbon in its own section.
    /// </summary>
    private static string BuildPresentExtraData(PresentGrantRequest request, string productCode)
    {
        var tag = new Dictionary<string, string>
        {
            [PresentData.MESSAGE] = request.Message,
            [PresentData.PRODUCT_CODE] = productCode,
            [PresentData.EXTRA_PARAM] = string.Empty,
        };

        if (request.PurchaserName is { } name)
            tag[PresentData.PURCHASER_NAME] = name;

        if (request.PurchaserFigure is { } figure)
            tag[PresentData.PURCHASER_FIGURE] = figure;

        return JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                [ExtraDataSectionType.STUFF] = new { Data = tag },
                [PresentStorage.SECTION] = new PresentStorage
                {
                    BoxType = request.BoxType,
                    RibbonType = request.RibbonType,
                },
            }
        );
    }
}
