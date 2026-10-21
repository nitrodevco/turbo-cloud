using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Catalog.Providers;
using Turbo.Database.Entities.Assets;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Gamedata;
using Turbo.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.Habbo;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Players;
using Turbo.Tests.Catalog;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// The furniture list the panel shows every definition in, each with its tags: the hotel's own,
/// Habbo's update waiting, edited here, without a bundle (or with one that failed or is behind
/// Habbo's), and sold nowhere players can reach. The tags follow what changes them.
/// </summary>
public sealed class FurnitureListTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);

    private const int PAGE = 2;
    private const int SOFA = 900;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly GamedataFurnitureService _furniture;
    private int _bundles;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public FurnitureListTests()
    {
        var config = Options.Create(new GamedataConfig());
        var definitions = _fakes.Create<IFurnitureDefinitionProvider>();
        var offers = new FurnitureOfferCatalog(
            new CatalogSnapshotProvider<NormalCatalog>(
                _db,
                NullLogger<ICatalogSnapshotProvider<NormalCatalog>>.Instance,
                definitions,
                CatalogType.Normal
            ),
            new CatalogSnapshotProvider<BuildersClubCatalog>(
                _db,
                NullLogger<ICatalogSnapshotProvider<BuildersClubCatalog>>.Instance,
                definitions,
                CatalogType.BuildersClub
            )
        );
        var habboItems = new HabboReleaseItems(_db, config);
        var furnitureFiles = new HabboFurnitureFiles(
            _db,
            config,
            new HabboGamedataClient(
                _fakes.Create<System.Net.Http.IHttpClientFactory>(),
                NullLogger<HabboGamedataClient>.Instance
            ),
            NullLogger<HabboFurnitureFiles>.Instance
        );

        _furniture = new GamedataFurnitureService(
            _db,
            config,
            definitions,
            _fakes.Create<IGamedataFileService>(),
            offers,
            habboItems,
            furnitureFiles,
            new FurnitureStatusIndex(_db, habboItems, furnitureFiles, offers),
            new GamedataWriteLock(),
            TimeProvider.System,
            NullLogger<GamedataFurnitureService>.Instance
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task each_definition_is_tagged_with_what_it_lacks_and_what_waits_for_it()
    {
        await _furniture.ImportAsync(
            Release(1, Floor(30, "chair_norja", "Chair"), Floor(31, "lamp", "Lamp"), Poster()),
            STAFF,
            Ct
        );

        var ids = await IdsAsync();

        // The hotel's own sofa, its bundle failed; and a catalog that sells the chair alone and
        // the lamp only in a pack with it. The sofa's offer is hidden, so it is sold nowhere.
        _db.Insert(OwnSofa());
        _db.Insert(CatalogFixture.Page(1, null, "root"));
        _db.Insert(CatalogFixture.Page(PAGE, 1, "Furniture"));
        Offer(100, visible: true, ids["chair_norja"]);
        Offer(101, visible: true, ids["lamp"], ids["chair_norja"]);
        Offer(102, visible: false, SOFA);
        Bundle("chair_norja", revision: "61856");
        Bundle("lamp", revision: "99");
        Bundle("poster5", revision: "45508");
        Bundle("my_sofa", revision: null, error: "It would not convert.");

        (await TagsAsync())
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, FurnitureTag[]>
                {
                    ["chair_norja"] = [],
                    ["lamp"] = [FurnitureTag.BundleOutdated],
                    // One poster item, drawn from each poster's own bundle.
                    ["poster"] = [FurnitureTag.NotSold],
                    ["my_sofa"] =
                    [
                        FurnitureTag.HotelOwn,
                        FurnitureTag.NoBundle,
                        FurnitureTag.BundleFailed,
                        FurnitureTag.NotSold,
                    ],
                },
                o => o.WithStrictOrdering()
            );

        // The hotel changes the chair's name; Habbo's next release changes the lamp's.
        await _furniture.UpdateDefinitionAsync(
            ids["chair_norja"],
            new JsonObject { ["name"] = "Our chair" },
            STAFF,
            Ct
        );
        Release(2, Floor(30, "chair_norja", "Chair"), Floor(31, "lamp", "Bright lamp"), Poster());

        var tags = await TagsAsync();

        tags["chair_norja"].Should().Equal(FurnitureTag.Edited);
        tags["lamp"].Should().Equal(FurnitureTag.HabboUpdate, FurnitureTag.BundleOutdated);

        // Each definition says what its bundle is, so the list shows bundles beside furniture.
        var listed = (await _furniture.ListAsync(null, null, null, 0, Ct)).Items.ToDictionary(x =>
            x.ClassName
        );

        listed["chair_norja"]
            .Should()
            .Match<Turbo.Primitives.Gamedata.Snapshots.FurnitureListItem>(x =>
                x.BundleRevision == "61856"
                && x.BundleSize == 10
                && x.BundleSource == AssetBundleSource.Habbo
            );
        listed["my_sofa"]
            .Should()
            .Match<Turbo.Primitives.Gamedata.Snapshots.FurnitureListItem>(x =>
                x.BundleRevision == null && x.BundleSize == null && x.BundleSource == null
            );

        var detail = await _furniture.GetDefinitionAsync(ids["chair_norja"], Ct);

        detail!.Tags.Should().Equal(FurnitureTag.Edited);
        detail.AssetName.Should().Be("chair_norja");
    }

    [Fact]
    public async Task the_list_is_found_by_words_kind_id_and_tag_with_each_tag_counted()
    {
        await _furniture.ImportAsync(
            Release(1, Floor(30, "chair_norja", "Chair"), Floor(31, "lamp", "Lamp"), Poster()),
            STAFF,
            Ct
        );
        _db.Insert(OwnSofa());

        var all = await _furniture.ListAsync(null, null, null, 0, Ct);

        all.Items.Select(x => x.ClassName)
            .Should()
            .Equal("chair_norja", "lamp", "my_sofa", "poster");
        all.Total.Should().Be(4);
        all.Searched.Should().Be(4);
        all.TagCounts[FurnitureTag.NotSold].Should().Be(4);
        all.TagCounts[FurnitureTag.HotelOwn].Should().Be(1);
        all.TagCounts.Should().NotContainKey(FurnitureTag.HabboUpdate);

        (await _furniture.ListAsync("SOFA", null, null, 0, Ct))
            .Items.Select(x => x.ClassName)
            .Should()
            .Equal("my_sofa");
        (await _furniture.ListAsync("bright", null, null, 0, Ct)).Items.Should().BeEmpty();
        (await _furniture.ListAsync("31", null, null, 0, Ct))
            .Items.Should()
            .ContainSingle()
            .Which.ClassName.Should()
            .Be("lamp");
        (await _furniture.ListAsync(null, ProductType.Wall, null, 0, Ct))
            .Items.Select(x => x.ClassName)
            .Should()
            .Equal("poster");

        // A tag narrows the list; the counts stay those of everything the words found.
        var own = await _furniture.ListAsync(null, null, FurnitureTag.HotelOwn, 0, Ct);

        own.Items.Select(x => x.ClassName).Should().Equal("my_sofa");
        own.Total.Should().Be(1);
        own.Searched.Should().Be(4);
        own.TagCounts[FurnitureTag.NotSold].Should().Be(4);
    }

    private async Task<Dictionary<string, FurnitureTag[]>> TagsAsync() =>
        (await _furniture.ListAsync(null, null, null, 0, Ct)).Items.ToDictionary(
            x => x.ClassName,
            x => x.Tags.ToArray()
        );

    private async Task<Dictionary<string, int>> IdsAsync()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        return await db.FurnitureDefinitions.ToDictionaryAsync(x => x.Name, x => x.Id, Ct);
    }

    private void Offer(int id, bool visible, params int[] definitionIds)
    {
        _db.Insert(
            new CatalogOfferEntity
            {
                Id = id,
                CatalogPageEntityId = PAGE,
                LocalizationId = "offer",
                CostCredits = 1,
                CostCurrency = 0,
                CanGift = true,
                CanBundle = true,
                ClubLevel = 0,
                Visible = visible,
                Page = null!,
            }
        );

        foreach (var (definitionId, index) in definitionIds.Select((x, i) => (x, i)))
            _db.Insert(
                new CatalogProductEntity
                {
                    Id = (id * 10) + index,
                    CatalogOfferEntityId = id,
                    ProductType = ProductType.Floor,
                    FurnitureDefinitionEntityId = definitionId,
                    Quantity = 1,
                    Offer = null!,
                }
            );
    }

    private void Bundle(string name, string? revision, string? error = null) =>
        _db.Insert(
            new AssetBundleEntity
            {
                Id = ++_bundles,
                Kind = AssetBundleKind.Furniture,
                Name = name,
                Revision = revision,
                Source = AssetBundleSource.Habbo,
                Hash = error is null ? "hash" : null,
                Size = error is null ? 10 : 0,
                Error = error,
                UpdatedAt = DateTime.UtcNow,
            }
        );

    private static FurnitureDefinitionEntity OwnSofa() =>
        new()
        {
            Id = SOFA,
            SpriteId = SOFA,
            Name = "my_sofa",
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = "default_floor",
            Width = 2,
            Length = 1,
            StackHeight = 1,
            CanStack = false,
            CanWalk = false,
            CanSit = true,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
            PublicName = "My sofa",
        };

    private int Release(int id, params JsonObject[] items)
    {
        var floor = new JsonArray();
        var wall = new JsonArray();

        foreach (var item in items)
            (item.ContainsKey("xdim") ? floor : wall).Add(item);

        var data = Encoding.UTF8.GetBytes(
            new JsonObject
            {
                ["roomitemtypes"] = new JsonObject { ["furnitype"] = floor },
                ["wallitemtypes"] = new JsonObject { ["furnitype"] = wall },
            }.ToJsonString()
        );

        _db.Insert(
            new HabboReleaseEntity
            {
                Id = id,
                Domain = "com",
                Revision = $"PRODUCTION-{id}",
                FurnitureDataHash = GamedataBytes.Hash(data),
                FurnitureData = GamedataBytes.Compress(data),
                FurnitureCount = items.Length,
                CheckedAt = DateTime.UtcNow,
            }
        );

        return id;
    }

    private static JsonObject Floor(int id, string className, string name) =>
        new()
        {
            ["id"] = id,
            ["classname"] = className,
            ["revision"] = className == "lamp" ? 100 : 61856,
            ["category"] = "chair",
            ["defaultdir"] = 0,
            ["xdim"] = 1,
            ["ydim"] = 1,
            ["partcolors"] = new JsonObject { ["color"] = new JsonArray() },
            ["name"] = name,
            ["description"] = "",
            ["adurl"] = "",
            ["offerid"] = -1,
            ["buyout"] = false,
            ["rentofferid"] = -1,
            ["rentbuyout"] = false,
            ["bc"] = false,
            ["excludeddynamic"] = false,
            ["bcofferid"] = -1,
            ["customparams"] = "",
            ["specialtype"] = 1,
            ["canstandon"] = false,
            ["cansiton"] = true,
            ["canlayon"] = false,
            ["canputstuffon"] = false,
            ["height"] = 1.0,
            ["furniline"] = "iced",
            ["environment"] = "",
            ["rare"] = false,
            ["tradeable"] = true,
            ["recyclable"] = true,
        };

    private static JsonObject Poster() =>
        new()
        {
            ["id"] = 4001,
            ["classname"] = "poster",
            ["revision"] = 45508,
            ["category"] = "wall_decoration",
            ["name"] = "Poster",
            ["description"] = "",
            ["adurl"] = "",
            ["specialtype"] = 6,
            ["furniline"] = "",
            ["environment"] = "",
            ["rare"] = false,
            ["tradeable"] = true,
            ["recyclable"] = true,
            ["offerid"] = -1,
            ["buyout"] = false,
            ["rentofferid"] = -1,
            ["rentbuyout"] = false,
            ["bc"] = false,
            ["excludeddynamic"] = false,
            ["bcofferid"] = -1,
        };
}
