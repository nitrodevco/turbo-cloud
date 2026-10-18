using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Crafting;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Crafting;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A crafting table: the client's <c>furniture_crafting_gizmo</c>, whose widget (Flash
/// <c>CraftingWidgetHandler</c>) lists the table's recipes, shows what one takes and crafts it from
/// the player's inventory, or mixes inventory items to find a secret recipe. What it makes is its
/// definition's <see cref="CraftingData"/>.
/// <para>
/// Anyone may look; only the table's owner crafts (crafting.btn.notowner: "You can only craft items
/// using your own crafting table."). Rented furni are never ingredients (the widget lists only
/// <c>getNonRentedInventoryIds</c>). A craft uses its ingredients up and puts the product in the
/// inventory, whose "list changed" is what the widget waits for to ask for the table again
/// (<c>registerForFurniListInvalidate</c>).
/// </para>
/// </summary>
[RoomObjectLogic("crafting")]
public class FurnitureCraftingLogic : FurnitureFloorLogic
{
    private readonly CraftingData _data;

    public FurnitureCraftingLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
        : base(stuffDataFactory, ctx)
    {
        _data =
            FurnitureExtraDataSections.Read<CraftingData>(
                ctx.RoomObject.ExtraData,
                ctx.Definition.ExtraData,
                CraftingData.SECTION,
                _roomGrain._logger
            ) ?? new CraftingData();
    }

    /// <summary>The listed (not secret) recipe of this code, if the table makes it.</summary>
    public CraftingRecipeData? FindListedRecipe(string code) =>
        _data.Recipes.FirstOrDefault(x => !x.Secret && x.Code == code);

    public static ImmutableArray<CraftingIngredientSnapshot> Ingredients(
        CraftingRecipeData recipe
    ) =>
        [
            .. recipe.Ingredients.Select(x => new CraftingIngredientSnapshot
            {
                Count = x.Amount,
                FurnitureClassName = x.Furni,
            }),
        ];

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        switch (interaction)
        {
            case GetCraftableProductsInteraction:
                await SendAsync(
                    ctx,
                    new CraftableProductsMessageComposer
                    {
                        Products = [.. _data.Recipes.Where(x => !x.Secret).Select(Product)],
                        UsableFurniClasses =
                        [
                            .. _data
                                .Recipes.SelectMany(x => x.Ingredients)
                                .Select(x => x.Furni)
                                .Distinct(),
                        ],
                    },
                    ct
                );

                return true;
            case CraftInteraction craft:
                return await CraftListedAsync(ctx, craft, ct);
            case CraftingMixerInteraction mixer:
                return await MixAsync(ctx, mixer, ct);
            default:
                return await base.OnInteractAsync(ctx, interaction, ct);
        }
    }

    private async Task<bool> CraftListedAsync(
        ActionContext ctx,
        CraftInteraction craft,
        CancellationToken ct
    )
    {
        if (!IsItemOwner(ctx))
            return await FailAsync(ctx, craft, "not the table's owner", ct);

        if (FindListedRecipe(craft.RecipeCode) is not { } recipe)
            return await FailAsync(ctx, craft, $"no recipe {craft.RecipeCode}", ct);

        var held = await _roomGrain
            ._grainFactory.GetInventoryGrain(ctx.PlayerId)
            .GetAllItemSnapshotsAsync(ct);
        var used = new List<FurnitureItemSnapshot>();

        foreach (var ingredient in recipe.Ingredients)
        {
            var items = held.Where(x => x.Definition.Name == ingredient.Furni && !IsRented(x))
                .Where(x => !used.Contains(x))
                .Take(ingredient.Amount)
                .ToList();

            if (items.Count < ingredient.Amount)
                return await FailAsync(ctx, craft, $"missing {ingredient.Furni}", ct);

            used.AddRange(items);
        }

        return await CraftAsync(ctx, craft, recipe, used, ct);
    }

    private async Task<bool> MixAsync(
        ActionContext ctx,
        CraftingMixerInteraction mixer,
        CancellationToken ct
    )
    {
        var ids = mixer.ItemIds.Distinct().ToImmutableArray();
        var items = await _roomGrain
            ._grainFactory.GetInventoryGrain(ctx.PlayerId)
            .GetItemSnapshotsAsync(ids, ct);

        if (items.Length != ids.Length || items.Any(IsRented))
            items = [];

        var mixed = Counts(items.Select(x => x.Definition.Name));
        var secrets = _data.Recipes.Where(x => x.Secret).ToList();
        var exact = items.IsEmpty
            ? null
            : secrets.FirstOrDefault(x => SameCounts(Counts(Ingredients(x)), mixed));

        if (!mixer.Craft)
        {
            // CraftingInfoController.craftingSecretRecipesAvailable: complete with no others
            // "found a secret recipe", complete with others "you can also try N others", else
            // "N of recipes have this" or "no recipes found". Inference: the others are the secret
            // recipes the mix is part of, the exact one left out.
            var others = items.IsEmpty
                ? 0
                : secrets.Count(x => x != exact && Contains(Counts(Ingredients(x)), mixed));

            await SendAsync(
                ctx,
                new CraftingRecipesAvailableMessageComposer
                {
                    Count = others,
                    RecipeComplete = exact is not null,
                },
                ct
            );

            return true;
        }

        if (!IsItemOwner(ctx))
            return await FailAsync(ctx, mixer, "not the table's owner", ct);

        if (exact is null)
            return await FailAsync(ctx, mixer, "the mix makes no secret recipe", ct);

        return await CraftAsync(ctx, mixer, exact, [.. items], ct);
    }

    private async Task<bool> CraftAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CraftingRecipeData recipe,
        IReadOnlyList<FurnitureItemSnapshot> ingredients,
        CancellationToken ct
    )
    {
        if (
            _roomGrain._definitionProvider.TryGetDefinitionByName(recipe.Product) is not { } product
        )
            return await FailAsync(ctx, interaction, $"no furni {recipe.Product}", ct);

        var inventory = _roomGrain._grainFactory.GetInventoryGrain(ctx.PlayerId);
        // One operation: each ingredient used up in turn, then the product.
        var cause = MovementCause.New(
            MovementCauses.USE_CRAFTING,
            ctx.PlayerId.Value,
            _ctx.ObjectId.Value
        );
        var seq = 0;

        foreach (var item in ingredients)
        {
            if (
                await inventory.ConsumeFurnitureAsync(item.ItemId, cause.StartingAt(seq++), ct)
                is null
            )
            {
                _roomGrain._logger.LogError(
                    "Crafting {Recipe} on item {ItemId} in room {RoomId}: ingredient {Ingredient} of player {PlayerId} went missing mid-craft",
                    recipe.Code,
                    _ctx.ObjectId,
                    _ctx.RoomId,
                    item.ItemId,
                    ctx.PlayerId
                );

                return await FailAsync(ctx, interaction, "an ingredient went missing", ct);
            }
        }

        await inventory.GrantFurnitureAsync(product.Id, null, cause.StartingAt(seq), ct);

        await SendAsync(
            ctx,
            new CraftingResultMessageComposer { Success = true, Product = Product(recipe) },
            ct
        );

        return true;
    }

    private async Task<bool> FailAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        string reason,
        CancellationToken ct
    )
    {
        // Flash's CraftingWidgetHandler.onCraftingResultMessage clears the mixer and shows the
        // table again on a failed craft; unanswered, the widget waits on its progress bar.
        await SendAsync(ctx, new CraftingResultMessageComposer { Success = false }, ct);

        return Reject(ctx, interaction, reason);
    }

    private Task SendAsync(ActionContext ctx, IComposer composer, CancellationToken ct) =>
        _roomGrain._grainFactory.SendComposerToPlayerAsync(ctx.PlayerId, composer, ct);

    private static CraftableProductSnapshot Product(CraftingRecipeData recipe) =>
        new()
        {
            RecipeCode = recipe.Code,
            ProductCode = string.IsNullOrEmpty(recipe.ProductCode)
                ? recipe.Product
                : recipe.ProductCode,
            FurnitureClassName = recipe.Product,
        };

    /// <summary>Rental furni carry their expiry; owned ones -1.</summary>
    private static bool IsRented(FurnitureItemSnapshot item) => item.SecondsToExpiration >= 0;

    private static Dictionary<string, int> Counts(IEnumerable<string> classes) =>
        classes.GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());

    private static Dictionary<string, int> Counts(
        ImmutableArray<CraftingIngredientSnapshot> ingredients
    ) =>
        ingredients
            .GroupBy(x => x.FurnitureClassName)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Count));

    private static bool SameCounts(Dictionary<string, int> a, Dictionary<string, int> b) =>
        a.Count == b.Count && a.All(x => b.GetValueOrDefault(x.Key) == x.Value);

    /// <summary>Whether <paramref name="recipe"/> holds at least every item of <paramref name="mix"/>.</summary>
    private static bool Contains(Dictionary<string, int> recipe, Dictionary<string, int> mix) =>
        mix.All(x => recipe.GetValueOrDefault(x.Key) >= x.Value);
}
