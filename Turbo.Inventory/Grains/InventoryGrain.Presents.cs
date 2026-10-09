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

        var tag = new Dictionary<string, string>
        {
            [PresentData.MESSAGE] = request.Message,
            [PresentData.PRODUCT_CODE] = content.Name,
            [PresentData.EXTRA_PARAM] = string.Empty,
        };

        if (request.PurchaserName is { } name)
            tag[PresentData.PURCHASER_NAME] = name;

        if (request.PurchaserFigure is { } figure)
            tag[PresentData.PURCHASER_FIGURE] = figure;

        await FurniModule.GrantPresentAsync(
            present,
            BuildPresentExtraData(
                tag,
                new PresentStorage { BoxType = request.BoxType, RibbonType = request.RibbonType }
            ),
            content,
            contentExtraData,
            ct
        );
    }

    /// <summary>
    /// A gift from the hotel: the tag names no sender, so the client shows its "Special Gift"
    /// card with no face, and says the sender is trusted only when asked to. A badge it gives is
    /// kept in the present's own section, out of the client's sight.
    /// </summary>
    public Task<FurnitureItemSnapshot> ReceiveStaffPresentAsync(
        StaffPresentGrantRequest request,
        CancellationToken ct
    )
    {
        var content = FurniModule.GetDefinitionOrThrow(request.FurniDefinitionId);
        var present = FurniModule.GetDefinitionOrThrow(request.PresentDefinitionId);
        var tag = new Dictionary<string, string>
        {
            [PresentData.MESSAGE] = request.Message,
            [PresentData.PRODUCT_CODE] = content.Name,
            [PresentData.EXTRA_PARAM] = string.Empty,
        };

        if (request.TrustedSender)
            tag[PresentData.TRUSTED_SENDER] = PresentData.TRUSTED;

        return FurniModule.GrantPresentAsync(
            present,
            BuildPresentExtraData(
                tag,
                new PresentStorage
                {
                    BoxType = 0,
                    RibbonType = 0,
                    BadgeCode = request.BadgeCode,
                }
            ),
            content,
            null,
            ct
        );
    }

    public Task<FurnitureItemSnapshot?> UnwrapPresentAsync(
        RoomObjectId presentId,
        CancellationToken ct
    ) => FurniModule.UnwrapPresentAsync(presentId, ct);

    /// <summary>
    /// What the present carries: the tag the client's present logic reads from its map data,
    /// and the box, ribbon and anything else for the server alone in its own section.
    /// </summary>
    private static string BuildPresentExtraData(
        Dictionary<string, string> tag,
        PresentStorage storage
    ) =>
        JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                [ExtraDataSectionType.STUFF] = new { Data = tag },
                [PresentStorage.SECTION] = storage,
            }
        );
}
