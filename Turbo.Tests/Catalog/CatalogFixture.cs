using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Catalog.Providers;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Extensions;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Tests.Support;

namespace Turbo.Tests.Catalog;

/// <summary>
/// A small catalog in a relational database, as the hotel's is laid out: the currency rows the
/// hotel ships (duckets are row 4 but activity-point type 0), a floor and a wall item, and one
/// tree of pages: a shown and an invisible tab, and under the shown one a normal page and a page
/// only the Builders Club catalog shows. The offers are one of each case the editor and the
/// purchase path care about.
/// </summary>
public sealed class CatalogFixture : IDisposable
{
    public const int ROOT = 1;
    public const int FURNITURE = 2;
    public const int HIDDEN_PAGE = 3;
    public const int BUILDERS_PAGE = 4;
    public const int CHILD = 5;

    public const int CHAIR = 10;
    public const int POSTER = 11;

    public const int CREDITS_ROW = 1;
    public const int DUCKETS_ROW = 4;
    public const int DIAMONDS_ROW = 5;

    public const int SOLD = 100;
    public const int HIDDEN_OFFER = 101;
    public const int ON_HIDDEN_PAGE = 102;
    public const int IN_DUCKETS = 103;
    public const int IN_DIAMONDS = 104;
    public const int IN_CREDITS_ROW = 105;

    private readonly FurnitureDefinitionEntity[] _definitions =
    [
        Definition(CHAIR, "chair", ProductType.Floor),
        Definition(POSTER, "poster", ProductType.Wall),
    ];

    public SqliteDb Db { get; } = new();

    public Fakes Fakes { get; } = new();

    public IFurnitureDefinitionProvider Definitions { get; }

    public CatalogFixture()
    {
        Fakes.Handlers["TryGetDefinition"] = call =>
            _definitions.FirstOrDefault(x => x.Id == (int)call.Args[0]!)?.ToSnapshot(0.01);
        Fakes.Handlers["TryGetDefinitionByName"] = call =>
            _definitions
                .FirstOrDefault(x =>
                    string.Equals(x.Name, (string)call.Args[0]!, StringComparison.OrdinalIgnoreCase)
                )
                ?.ToSnapshot(0.01);
        Fakes.Handlers["FindNames"] = call =>
            (IReadOnlyList<string>)
                [
                    .. _definitions
                        .Select(x => x.Name)
                        .Where(x =>
                            x.StartsWith((string)call.Args[0]!, StringComparison.OrdinalIgnoreCase)
                        ),
                ];
        Definitions = Fakes.Create<IFurnitureDefinitionProvider>();

        Db.Insert(Currency(CREDITS_ROW, "credits", CurrencyType.Credits, null));
        Db.Insert(Currency(DUCKETS_ROW, "duckets", CurrencyType.ActivityPoints, 0));
        Db.Insert(Currency(DIAMONDS_ROW, "diamonds", CurrencyType.ActivityPoints, 5));

        foreach (var definition in _definitions)
            Db.Insert(definition);

        Db.Insert(Page(ROOT, null, "root"));
        Db.Insert(Page(FURNITURE, ROOT, "Furniture", sort: 0));
        Db.Insert(Page(HIDDEN_PAGE, ROOT, "Secret", CatalogPageDisplay.Invisible, sort: 1));
        Db.Insert(Page(CHILD, FURNITURE, "Chairs"));
        Db.Insert(
            Page(BUILDERS_PAGE, FURNITURE, "Builders", CatalogPageDisplay.BuildersClubOnly, 1)
        );

        AddOffer(SOLD, FURNITURE, credits: 5);
        AddOffer(HIDDEN_OFFER, FURNITURE, credits: 5, visible: false);
        AddOffer(ON_HIDDEN_PAGE, HIDDEN_PAGE, credits: 5);
        AddOffer(IN_DUCKETS, FURNITURE, currency: 3, currencyRow: DUCKETS_ROW);
        AddOffer(IN_DIAMONDS, FURNITURE, currency: 2, currencyRow: DIAMONDS_ROW);
        AddOffer(IN_CREDITS_ROW, FURNITURE, currency: 7, currencyRow: CREDITS_ROW);
    }

    public void Dispose() => Db.Dispose();

    public CatalogSnapshotProvider<NormalCatalog> NormalProvider(
        CapturingLogger<ICatalogSnapshotProvider<NormalCatalog>>? log = null
    ) => new(Db, log ?? new(), Definitions, CatalogType.Normal);

    public CatalogSnapshotProvider<BuildersClubCatalog> BuildersClubProvider() =>
        new(
            Db,
            NullLogger<ICatalogSnapshotProvider<BuildersClubCatalog>>.Instance,
            Definitions,
            CatalogType.BuildersClub
        );

    public void AddOffer(
        int id,
        int pageId,
        int credits = 0,
        int currency = 0,
        int? currencyRow = null,
        bool visible = true
    )
    {
        Db.Insert(
            new CatalogOfferEntity
            {
                Id = id,
                CatalogPageEntityId = pageId,
                LocalizationId = "chair",
                CostCredits = credits,
                CostCurrency = currency,
                CurrencyTypeId = currencyRow,
                CanGift = true,
                CanBundle = true,
                ClubLevel = 0,
                Visible = visible,
                Page = null!,
            }
        );
        Db.Insert(
            new CatalogProductEntity
            {
                Id = id,
                CatalogOfferEntityId = id,
                ProductType = ProductType.Floor,
                FurnitureDefinitionEntityId = CHAIR,
                Quantity = 1,
                Offer = null!,
            }
        );
    }

    public static CatalogPageEntity Page(
        int id,
        int? parentId,
        string title,
        CatalogPageDisplay display = CatalogPageDisplay.Regular,
        int sort = 0
    ) =>
        new()
        {
            Id = id,
            ParentEntityId = parentId,
            Localization = title,
            Name = title.ToLowerInvariant(),
            Icon = 1,
            Layout = "default_3x3",
            SortOrder = sort,
            Display = display,
        };

    private static CurrencyTypeEntity Currency(
        int id,
        string name,
        CurrencyType type,
        int? activityPointType
    ) =>
        new()
        {
            Id = id,
            Name = name,
            CurrencyType = type,
            ActivityPointType = activityPointType,
            Enabled = true,
        };

    private static FurnitureDefinitionEntity Definition(int id, string name, ProductType type) =>
        new()
        {
            Id = id,
            SpriteId = id,
            Name = name,
            ProductType = type,
            FurniCategory = FurnitureCategory.Default,
            Logic = "default_floor",
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
        };
}
