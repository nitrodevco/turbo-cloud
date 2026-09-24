using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Guilds;

namespace Turbo.Inventory.Grains;

internal sealed partial class InventoryGrain
{
    /// <summary>
    /// Hands a bought offer to the sections its products belong to. Everything that can refuse
    /// the purchase (a missing definition, a bad pet name) is checked before anything is
    /// created, so a refusal leaves nothing behind.
    /// </summary>
    public async Task GrantCatalogOfferAsync(
        CatalogOfferSnapshot offer,
        string extraParam,
        int quantity,
        CancellationToken ct
    )
    {
        quantity = Math.Max(1, quantity);

        var furniture = new List<(FurnitureDefinitionSnapshot, string?)>();
        var pets = new List<Modules.PetProductGrant>();
        var bots = new List<Modules.BotProductGrant>();

        foreach (var product in offer.Products)
        {
            switch (product.ProductType)
            {
                case ProductType.Floor:
                case ProductType.Wall:
                {
                    var definition = _furniModule.GetDefinitionOrThrow(product.FurniDefinitionId);

                    // Guild furni is bought for a group: the item carries the group id and looks
                    // the badge and the colours up from it when it attaches, so nothing stale is
                    // written here. The purchase grain has already checked the buyer is in it.
                    var extraDataJson = GuildFurnitureLogicNames.IsGuildFurniture(
                        definition.LogicName
                    )
                        ? BuildGuildFurnitureExtraData(extraParam)
                        : null;

                    for (var i = 0; i < quantity; i++)
                        furniture.Add((definition, extraDataJson));

                    break;
                }
                case ProductType.Pet:
                    pets.Add(_petModule.ValidateProduct(offer, product, extraParam));
                    break;
                case ProductType.Robot:
                    bots.Add(_botModule.ValidateProduct(offer, product));
                    break;
            }
        }

        foreach (var pet in pets)
            await _petModule.GrantProductAsync(pet, ct);

        foreach (var bot in bots)
            await _botModule.GrantProductAsync(bot, ct);

        await _furniModule.GrantAsync(furniture, ct);
    }

    /// <summary>
    /// The extra data a newly bought piece of guild furni carries: the state it starts in and
    /// the group it belongs to. The badge and the two colours are left empty on purpose — the
    /// item looks them up from the group when it attaches, so nothing written here can go stale
    /// when the group is edited.
    /// </summary>
    private static string BuildGuildFurnitureExtraData(string extraParam)
    {
        var guildId = int.TryParse(extraParam, out var parsed) && parsed > 0 ? parsed : 0;

        return JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                [ExtraDataSectionType.STUFF] = new
                {
                    Data = new[]
                    {
                        "0",
                        guildId.ToString(CultureInfo.InvariantCulture),
                        string.Empty,
                        string.Empty,
                        string.Empty,
                    },
                },
            }
        );
    }
}
