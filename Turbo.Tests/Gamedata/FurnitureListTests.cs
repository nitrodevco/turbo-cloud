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
using Turbo.Primitives.Rooms.Providers;
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

        _fakes.Handlers["get_" + nameof(IRoomObjectLogicProvider.LogicTypes)] = _ =>
            (IReadOnlyList<string>)[];
        _furniture = new GamedataFurnitureService(
            _db,
            config,
            definitions,
            _fakes.Create<IGamedataFileService>(),
            offers,
            habboItems,
            furnitureFiles,
            new FurnitureStatusIndex(
                _db,
                habboItems,
                furnitureFiles,
                offers,
                _fakes.Create<IRoomObjectLogicProvider>()
            ),
            new GamedataWriteLock(),
            _fakes.Create<IRoomObjectLogicProvider>(),
            _fakes.Create<IAssetBundleStore>(),
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

    // Live 2026-10-26: guild_forum and the gld_* furniture kept default_floor (made before
    // definitions took the logic registered under their classname), so the forum terminal was
    // sold as plain furniture and opened no forum. The list flags each, and one call fixes them.
    [Fact]
    public async Task a_definition_running_the_default_where_the_server_has_its_logic_is_flagged_and_fixed()
    {
        _fakes.Handlers["get_" + nameof(IRoomObjectLogicProvider.LogicTypes)] = _ =>
            (IReadOnlyList<string>)["gate", "guild_customized", "guild_forum"];
        _db.Insert(Own(1, "guild_forum", "default_floor"));
        _db.Insert(Own(2, "gld_carpet", "default_floor"));
        _db.Insert(Own(3, "gld_gate", "gate"));
        _db.Insert(Own(4, "chair", "default_floor"));
        _db.Insert(Own(5, "guild_forum*2", "no_such_logic"));
        // Only an asset file tells the gld_* furniture's logic: its client logic.
        _db.Insert(AssetFile(1, "gld_carpet", 10, "furniture_guild_customized"));
        _db.Insert(AssetFile(2, "gld_gate", 10, "furniture_guild_customized"));

        var listed = (await _furniture.ListAsync(null, null, null, 0, Ct)).Items.ToDictionary(x =>
            x.ClassName
        );

        listed
            .Where(x => x.Value.Tags.Contains(FurnitureTag.LogicMismatch))
            .Select(x => (x.Key, x.Value.Logic, x.Value.MatchingLogic))
            .Should()
            .BeEquivalentTo([
                ("guild_forum", "default_floor", "guild_forum"),
                ("gld_carpet", "default_floor", "guild_customized"),
                ("guild_forum*2", "no_such_logic", "guild_forum"),
            ]);
        // A logic of the server's staff chose over the matching one is theirs to keep.
        listed["gld_gate"].MatchingLogic.Should().Be("guild_customized");
        listed["chair"].MatchingLogic.Should().BeNull();

        var fixedAll = await _furniture.SetLogicAsync(null, null, STAFF, Ct);

        fixedAll!.ChangeCount.Should().Be(3);
        (await TagsAsync())
            .Values.Should()
            .NotContain(tags => tags.Contains(FurnitureTag.LogicMismatch));
        (await LogicsAsync())
            .Should()
            .Contain(
                new Dictionary<string, string>
                {
                    ["guild_forum"] = "guild_forum",
                    ["gld_carpet"] = "guild_customized",
                    ["guild_forum*2"] = "guild_forum",
                    ["gld_gate"] = "gate",
                    ["chair"] = "default_floor",
                }
            );

        // Chosen definitions take a chosen logic; a logic the server lacks, or one for every
        // definition, is refused.
        (await _furniture.SetLogicAsync([4], "gate", STAFF, Ct))!
            .ChangeCount.Should()
            .Be(1);
        (await LogicsAsync())["chair"].Should().Be("gate");
        await _furniture
            .Invoking(x => x.SetLogicAsync([4], "no_such_logic", STAFF, Ct))
            .Should()
            .ThrowAsync<ArgumentException>();
        await _furniture
            .Invoking(x => x.SetLogicAsync(null, "gate", STAFF, Ct))
            .Should()
            .ThrowAsync<ArgumentException>();
    }

    // The fridges, gates and teleports of a hotel installed before the import read the client
    // category (2026-10-29) run the default: the list flags them, and one call fixes them.
    [Fact]
    public async Task a_definition_whose_client_category_or_client_logic_the_server_has_a_logic_for_is_flagged()
    {
        _fakes.Handlers["get_" + nameof(IRoomObjectLogicProvider.LogicTypes)] = _ =>
            (IReadOnlyList<string>)
                ["gate", "vending_machine", "exchange", "one_way_door", "guild_customized"];
        _db.Insert(Categorised(Own(1, "country_gate", "default_floor"), "gate"));
        _db.Insert(Categorised(Own(2, "fridge*2", "default_floor"), "vending_machine"));
        _db.Insert(Categorised(Own(3, "CF_50_goldbar", "default_floor"), "credit"));
        // The client logic wins over the category, and a chosen logic stands.
        _db.Insert(Categorised(Own(4, "one_way_door", "default_floor"), "gate"));
        _db.Insert(Categorised(Own(5, "gld_gate", "guild_customized"), "gate"));
        _db.Insert(Categorised(Own(6, "chair", "default_floor"), "chair"));
        _db.Insert(AssetFile(1, "CF_50_goldbar", 10, "furniture_credit"));
        _db.Insert(AssetFile(2, "one_way_door", 10, "furniture_one_way_door"));

        var listed = (await _furniture.ListAsync(null, null, null, 0, Ct)).Items.ToDictionary(x =>
            x.ClassName
        );

        listed
            .Where(x => x.Value.Tags.Contains(FurnitureTag.LogicMismatch))
            .Select(x => (x.Key, x.Value.MatchingLogic))
            .Should()
            .BeEquivalentTo([
                ("country_gate", "gate"),
                ("fridge*2", "vending_machine"),
                ("CF_50_goldbar", "exchange"),
                ("one_way_door", "one_way_door"),
            ]);
        listed["gld_gate"].Tags.Should().NotContain(FurnitureTag.LogicMismatch);
        listed["chair"].MatchingLogic.Should().BeNull();

        (await _furniture.SetLogicAsync(null, null, STAFF, Ct))!.ChangeCount.Should().Be(4);
        (await LogicsAsync())["fridge*2"].Should().Be("vending_machine");
    }

    private static FurnitureDefinitionEntity Categorised(
        FurnitureDefinitionEntity definition,
        string category
    )
    {
        definition.ClientCategory = category;

        return definition;
    }

    private async Task<Dictionary<string, string>> LogicsAsync()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        return await db.FurnitureDefinitions.ToDictionaryAsync(x => x.Name, x => x.Logic, Ct);
    }

    private static FurnitureDefinitionEntity Own(int id, string className, string logic)
    {
        var definition = OwnSofa();

        definition.Id = id;
        definition.SpriteId = id;
        definition.Name = className;
        definition.Logic = logic;

        return definition;
    }

    private static HabboFurnitureAssetEntity AssetFile(
        int id,
        string asset,
        int revision,
        string logic
    ) =>
        new()
        {
            Id = id,
            AssetName = asset,
            Revision = revision,
            LogicType = logic,
        };

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
