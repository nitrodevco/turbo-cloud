using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Orleans;

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
        var teleportPairs = new List<FurnitureDefinitionSnapshot>();
        var pets = new List<Modules.PetProductGrant>();
        var bots = new List<Modules.BotProductGrant>();
        var effects = new Dictionary<int, long>();

        foreach (var product in offer.Products)
        {
            switch (product.ProductType)
            {
                case ProductType.Floor:
                case ProductType.Wall:
                {
                    var definition = FurniModule.GetDefinitionOrThrow(product.FurniDefinitionId);

                    // As in Habbo, one teleporter bought is a pair, linked to each other.
                    if (TeleportFurniture.IsTeleport(definition.LogicName))
                    {
                        for (var i = 0; i < quantity; i++)
                            teleportPairs.Add(definition);

                        break;
                    }

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
                    pets.Add(PetModule.ValidateProduct(offer, product, extraParam));
                    break;
                case ProductType.Robot:
                    bots.Add(BotModule.ValidateProduct(offer, product));
                    break;
                case ProductType.Effect:
                    // The id is the extra parameter and the quantity the copies. A product that
                    // names none was refused before the buyer was charged; failing here as well
                    // leaves the purchase refunded rather than silently giving nothing.
                    if (!EffectProducts.TryGetEffectId(product.ExtraParam, out var effectId))
                        throw new InvalidOperationException(
                            $"Effect product {product.Id} of offer {offer.Id} names no effect."
                        );

                    effects[effectId] =
                        effects.GetValueOrDefault(effectId) + ((long)product.Quantity * quantity);
                    break;
            }
        }

        foreach (var pet in pets)
            await PetModule.GrantProductAsync(pet, ct);

        foreach (var bot in bots)
            await BotModule.GrantProductAsync(bot, ct);

        await FurniModule.GrantAsync(furniture, ct);
        await FurniModule.GrantTeleportPairsAsync(teleportPairs, ct);

        // The check before the charge makes a refusal here a race (a second purchase in flight);
        // it still throws, so the buyer is refunded instead of charged for nothing.
        foreach (var (effectId, copies) in effects)
        {
            var result = await _grainFactory
                .GetPlayerEffectGrain(PlayerId)
                .GiveEffectAsync(effectId, 0, (int)Math.Min(copies, int.MaxValue), false, ct);

            if (result != EffectGrantResult.Granted)
                throw new InvalidOperationException(
                    $"Effect {effectId} could not be given to player {PlayerId}: {result}."
                );
        }
    }

    /// <summary>
    /// The extra data a newly bought piece of guild furni carries: the state it starts in and
    /// the group it belongs to. The badge and the two colours are left empty on purpose — the
    /// item looks them up from the group when it attaches, so nothing written here can go stale
    /// when the group is edited.
    /// </summary>
    private static string BuildGuildFurnitureExtraData(string extraParam)
    {
        var guildId =
            int.TryParse(
                extraParam,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed
            )
            && parsed > 0
                ? parsed
                : 0;

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
