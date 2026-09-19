using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;

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

                    for (var i = 0; i < quantity; i++)
                        furniture.Add((definition, null));

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
}
