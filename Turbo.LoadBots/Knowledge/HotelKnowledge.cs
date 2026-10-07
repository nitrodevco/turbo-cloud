using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.LoadBots.Knowledge;

/// <summary>A furni definition, as much of it as a bot needs to place the furni sensibly.</summary>
public sealed record FurniDefinition(
    int SpriteId,
    string Name,
    ProductType Type,
    string Logic,
    int Width,
    int Length,
    bool CanStack,
    bool CanWalk,
    bool CanSit,
    bool CanLay
)
{
    public bool IsWired => Logic.StartsWith(HotelKnowledge.WIRED_PREFIX, StringComparison.Ordinal);
}

/// <summary>A catalog offer of exactly one furni.</summary>
public sealed record FurniOffer(
    int PageId,
    int OfferId,
    int CostCredits,
    FurniDefinition Definition
);

/// <summary>
/// What a player would learn from the client's furnidata and from browsing: the room models,
/// what each sprite is, and which offers sell which furni. Read once from the database, read
/// only; everything a bot does with it still goes through the protocol.
/// </summary>
public sealed class HotelKnowledge
{
    public const string WIRED_PREFIX = "wf_";
    public const string DEFAULT_FLOOR_LOGIC = "default_floor";
    public const string GAME_COUNTER_LOGIC = "wf_game_upcounter1";

    /// <summary>What the game timers' and counter clocks' logics share (<c>wf_upcounter1</c>).</summary>
    private const string COUNTER_MARK = "upcounter";

    private readonly Dictionary<int, FurniDefinition> _floorBySprite;

    private HotelKnowledge(
        IReadOnlyList<string> roomModels,
        Dictionary<int, FurniDefinition> floorBySprite,
        IReadOnlyList<FurniOffer> offers
    )
    {
        RoomModels = roomModels;
        _floorBySprite = floorBySprite;

        DecorOffers =
        [
            .. offers.Where(x =>
                x.Definition.Logic == DEFAULT_FLOOR_LOGIC
                && x.Definition.Width <= 3
                && x.Definition.Length <= 3
            ),
        ];

        WalkablePadOffers =
        [
            .. offers.Where(x =>
                x.Definition.Logic == DEFAULT_FLOOR_LOGIC
                && x.Definition.CanWalk
                && x.Definition is { Width: 1, Length: 1 }
            ),
        ];

        // One offer per wired logic: the bots want every box once, not every colour of it. The
        // game timers and counter clocks share the prefix but are used, not configured.
        WiredOffers = offers
            .Where(x =>
                x.Definition.IsWired
                && !x.Definition.Logic.Contains(COUNTER_MARK, StringComparison.Ordinal)
            )
            .GroupBy(x => x.Definition.Logic, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(o => o.CostCredits).First(),
                StringComparer.Ordinal
            );

        GameCounterOffer = offers
            .Where(x => x.Definition.Logic == GAME_COUNTER_LOGIC)
            .OrderBy(x => x.CostCredits)
            .FirstOrDefault();
    }

    public IReadOnlyList<string> RoomModels { get; }

    /// <summary>Plain floor furni small enough to fit most rooms.</summary>
    public IReadOnlyList<FurniOffer> DecorOffers { get; }

    /// <summary>Plain 1×1 floor furni an avatar can stand on: game pads.</summary>
    public IReadOnlyList<FurniOffer> WalkablePadOffers { get; }

    /// <summary>Every buyable wired furni, keyed by its logic (<c>wf_trg_walks_on_furni</c>, ...).</summary>
    public IReadOnlyDictionary<string, FurniOffer> WiredOffers { get; }

    public FurniDefinition? FloorDefinition(int spriteId) =>
        _floorBySprite.GetValueOrDefault(spriteId);

    public FurniOffer? WiredOffer(string logic) => WiredOffers.GetValueOrDefault(logic);

    /// <summary>The game timer a host starts and ends rounds with.</summary>
    public FurniOffer? GameCounterOffer { get; }

    public static async Task<HotelKnowledge> LoadAsync(
        IDbContextFactory<TurboDbContext> dbFactory,
        CancellationToken ct
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var models = await db
            .RoomModels.AsNoTracking()
            .Where(x => x.Enabled && !x.Custom)
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync(ct);

        var definitions = await db
            .FurnitureDefinitions.AsNoTracking()
            .Where(x => x.ProductType == ProductType.Floor)
            .Select(x => new FurniDefinition(
                x.SpriteId,
                x.Name,
                x.ProductType,
                x.Logic,
                x.Width,
                x.Length,
                x.CanStack,
                x.CanWalk,
                x.CanSit,
                x.CanLay
            ))
            .ToListAsync(ct);

        var floorBySprite = new Dictionary<int, FurniDefinition>();

        foreach (var definition in definitions)
            floorBySprite.TryAdd(definition.SpriteId, definition);

        // Offers a player with no club and plenty of credits can buy from the normal catalog,
        // one floor furni each.
        var rows = await db
            .CatalogProducts.AsNoTracking()
            .Where(x =>
                x.FurnitureDefinitionEntityId != null
                && x.ProductType == ProductType.Floor
                && x.Quantity == 1
                && x.Offer.Visible
                && x.Offer.ClubLevel == 0
                && x.Offer.CostCurrency == 0
                && (
                    x.Offer.Page.Display == CatalogPageDisplay.Regular
                    || x.Offer.Page.Display == CatalogPageDisplay.Both
                )
                && x.Offer.Products!.Count == 1
            )
            .Select(x => new
            {
                x.Offer.CatalogPageEntityId,
                x.CatalogOfferEntityId,
                x.Offer.CostCredits,
                SpriteId = x.FurnitureDefinition!.SpriteId,
            })
            .ToListAsync(ct);

        var offers = rows.Where(x => floorBySprite.ContainsKey(x.SpriteId))
            .Select(x => new FurniOffer(
                x.CatalogPageEntityId,
                x.CatalogOfferEntityId,
                x.CostCredits,
                floorBySprite[x.SpriteId]
            ))
            .ToList();

        return new HotelKnowledge(models, floorBySprite, offers);
    }
}
