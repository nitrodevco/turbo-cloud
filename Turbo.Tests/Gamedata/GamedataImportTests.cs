using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Catalog.Providers;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Gamedata;
using Turbo.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.History;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// Habbo's furniture taken in: new items become definitions under Habbo's ids, Habbo's later
/// changes reach the fields the hotel left alone and not the ones it changed, and an import rolls
/// back as a whole - except what has changed again since.
/// </summary>
public sealed class GamedataImportTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly GamedataFurnitureService _furniture;
    private readonly GamedataHistoryService _history;
    private readonly IGamedataFileService _files;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GamedataImportTests()
    {
        var config = Options.Create(new GamedataConfig());
        var definitions = _fakes.Create<IFurnitureDefinitionProvider>();
        var writes = new GamedataWriteLock();
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

        _files = _fakes.Create<IGamedataFileService>();
        _furniture = new GamedataFurnitureService(
            _db,
            config,
            definitions,
            _files,
            offers,
            new HabboReleaseItems(_db, config),
            new HabboFurnitureFiles(
                _db,
                config,
                new HabboGamedataClient(
                    _fakes.Create<System.Net.Http.IHttpClientFactory>(),
                    NullLogger<HabboGamedataClient>.Instance
                ),
                NullLogger<HabboFurnitureFiles>.Instance
            ),
            writes,
            TimeProvider.System,
            NullLogger<GamedataFurnitureService>.Instance
        );
        _history = new GamedataHistoryService(
            _db,
            config,
            definitions,
            _files,
            writes,
            _fakes.Create<IHotelTextProvider>(),
            _fakes.Create<Turbo.Primitives.Figures.IFigureDataProvider>(),
            NullLogger<GamedataHistoryService>.Instance
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task habbos_new_furniture_becomes_definitions_under_habbos_ids()
    {
        var release = Release(1, Chair(xdim: 1), Poster());

        var preview = await _furniture.PreviewImportAsync(release, Ct);

        preview!.Added.Should().Be(2);

        var changeSet = await _furniture.ImportAsync(release, STAFF, Ct);

        changeSet!.Kind.Should().Be(GamedataChangeKind.Import);

        var definitions = await DefinitionsAsync();

        definitions.Should().HaveCount(2);

        var chair = definitions.Single(x => x.Name == "chair_norja");

        chair.SpriteId.Should().Be(30);
        chair.ProductType.Should().Be(ProductType.Floor);
        chair.CanSit.Should().BeTrue();
        chair.PublicName.Should().Be("Chair");
        chair.PartColors.Should().Equal("#ffffff", "#F7EBBC");

        // Habbo's heights have four decimals, and so does stack_height.
        chair.StackHeight.Should().Be(1.1125);
        definitions.Single(x => x.Name == "poster").ProductType.Should().Be(ProductType.Wall);
        _fakes.Log.Of(nameof(IGamedataFileService.Invalidate)).Should().NotBeEmpty();
    }

    [Fact]
    public async Task habbos_changes_reach_what_the_hotel_left_alone_and_not_what_it_changed()
    {
        await _furniture.ImportAsync(Release(1, Chair(xdim: 1)), STAFF, Ct);

        var chairId = (await DefinitionsAsync()).Single().Id;

        await _furniture.UpdateDefinitionAsync(
            chairId,
            new JsonObject { ["name"] = "Throne" },
            STAFF,
            Ct
        );

        var next = Release(2, Chair(xdim: 2, name: "Dining Chair"));
        var preview = await _furniture.PreviewImportAsync(next, Ct);

        preview!.Updated.Should().Be(1);
        preview.Items.Single().Fields.Should().Contain(x => x.Field == "name" && x.Kept);

        await _furniture.ImportAsync(next, STAFF, Ct);

        var chair = (await DefinitionsAsync()).Single();

        chair.Width.Should().Be(2);
        chair.PublicName.Should().Be("Throne");
    }

    [Fact]
    public async Task a_habbo_id_the_hotel_uses_for_its_own_furniture_gives_the_new_item_a_free_one()
    {
        _db.Insert(Definition(30, "my_own_chair"));

        await _furniture.ImportAsync(Release(1, Chair(xdim: 1)), STAFF, Ct);

        var chair = (await DefinitionsAsync()).Single(x => x.Name == "chair_norja");

        chair.SpriteId.Should().Be(31);
    }

    [Fact]
    public async Task rolling_an_import_back_removes_what_it_made_and_restores_what_it_changed()
    {
        await _furniture.ImportAsync(Release(1, Chair(xdim: 1)), STAFF, Ct);

        var second = await _furniture.ImportAsync(Release(2, Chair(xdim: 2), Poster()), STAFF, Ct);

        var result = await _history.RollbackAsync(second!.Id, STAFF, Ct);

        result!.Skipped.Should().BeEmpty();

        var definitions = await DefinitionsAsync();

        definitions.Should().ContainSingle().Which.Width.Should().Be(1);

        // Habbo's item is as the first import left it, so the next import sees Habbo's change again.
        var again = await _furniture.PreviewImportAsync(null, Ct);

        again!.Updated.Should().Be(1);
        again.Added.Should().Be(1);
    }

    [Fact]
    public async Task a_rollback_keeps_what_was_changed_again_since_and_cant_be_repeated()
    {
        await _furniture.ImportAsync(Release(1, Chair(xdim: 1)), STAFF, Ct);

        var second = await _furniture.ImportAsync(Release(2, Chair(xdim: 2)), STAFF, Ct);
        var chairId = (await DefinitionsAsync()).Single().Id;

        await _furniture.UpdateDefinitionAsync(chairId, new JsonObject { ["xdim"] = 4 }, STAFF, Ct);

        var result = await _history.RollbackAsync(second!.Id, STAFF, Ct);

        result!.Skipped.Should().Contain(x => x.Contains("xdim"));
        (await DefinitionsAsync()).Single().Width.Should().Be(4);

        var again = () => _history.RollbackAsync(second.Id, STAFF, Ct);

        await again.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task habbos_values_can_be_put_back_over_the_hotels_and_rolled_back()
    {
        await _furniture.ImportAsync(Release(1, Chair(xdim: 1), Poster()), STAFF, Ct);

        var chairId = (await DefinitionsAsync()).Single(x => x.Name == "chair_norja").Id;

        await _furniture.UpdateDefinitionAsync(
            chairId,
            new JsonObject
            {
                ["canputstuffon"] = true,
                ["recyclable"] = false,
                ["name"] = "Throne",
            },
            STAFF,
            Ct
        );

        var preview = await _furniture.PreviewHabboValuesAsync(["canputstuffon", "recyclable"], Ct);

        preview.Definitions.Should().Be(1);
        preview.ByField.Should().Contain("canputstuffon", 1).And.Contain("recyclable", 1);

        var changeSet = await _furniture.TakeHabboValuesAsync(
            ["canputstuffon", "recyclable"],
            STAFF,
            Ct
        );

        changeSet!.Kind.Should().Be(GamedataChangeKind.HabboValues);

        var chair = (await DefinitionsAsync()).Single(x => x.Id == chairId);

        chair.CanStack.Should().BeFalse();
        chair.CanRecycle.Should().BeTrue();
        // A field not chosen keeps the hotel's value.
        chair.PublicName.Should().Be("Throne");

        await _history.RollbackAsync(changeSet.Id, STAFF, Ct);

        chair = (await DefinitionsAsync()).Single(x => x.Id == chairId);
        chair.CanStack.Should().BeTrue();
        chair.CanRecycle.Should().BeFalse();
    }

    [Fact]
    public async Task the_states_its_file_gives_reach_the_definition_and_the_hotels_own_are_kept()
    {
        // The chair's file, read before: two states.
        _db.Insert(AssetFile(1, "chair_norja", 61856, states: 2));

        await _furniture.ImportAsync(Release(1, Chair(xdim: 1)), STAFF, Ct);

        var chair = (await DefinitionsAsync()).Single();

        chair.TotalStates.Should().Be(2);

        // The hotel gives it four; Habbo's next revision of the file says three.
        await _furniture.UpdateDefinitionAsync(
            chair.Id,
            new JsonObject { ["states"] = 4 },
            STAFF,
            Ct
        );
        _db.Insert(AssetFile(2, "chair_norja", 70000, states: 3));

        var next = Chair(xdim: 1);

        next["revision"] = 70000;

        var preview = await _furniture.PreviewImportAsync(Release(2, next), Ct);

        preview!.Items.Single().Fields.Should().Contain(x => x.Field == "states" && x.Kept);

        await _furniture.ImportAsync(2, STAFF, Ct);

        (await DefinitionsAsync()).Single().TotalStates.Should().Be(4);
    }

    [Fact]
    public async Task a_file_not_read_yet_leaves_states_alone_and_is_counted_to_read()
    {
        _db.Insert(Definition(30, "chair_norja"));

        var preview = await _furniture.PreviewImportAsync(Release(1, Chair(xdim: 1)), Ct);

        preview!.FilesToRead.Should().Be(1);
        preview.Items.SelectMany(x => x.Fields).Should().NotContain(x => x.Field == "states");
    }

    [Theory]
    [InlineData(false, 0)] // Habbo has no file, or it doesn't read: settled until its revision changes
    [InlineData(true, 1)] // its download failed: tried again
    public async Task a_file_that_failed_is_read_again_only_when_that_may_pass(
        bool retry,
        int toRead
    )
    {
        _db.Insert(
            new HabboFurnitureAssetEntity
            {
                AssetName = "chair_norja",
                Revision = 61856,
                Error = "Habbo has no file at its address.",
                Retry = retry,
            }
        );

        var preview = await _furniture.PreviewImportAsync(Release(1, Chair(xdim: 1)), Ct);

        preview!.FilesToRead.Should().Be(toRead);
    }

    [Fact]
    public async Task habbos_values_refuse_a_field_no_definition_holds()
    {
        var take = () => _furniture.TakeHabboValuesAsync(["offerid"], STAFF, Ct);

        await take.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task an_edit_refuses_a_field_a_definition_does_not_hold()
    {
        await _furniture.ImportAsync(Release(1, Poster()), STAFF, Ct);

        var posterId = (await DefinitionsAsync()).Single().Id;
        var edit = () =>
            _furniture.UpdateDefinitionAsync(posterId, new JsonObject { ["xdim"] = 2 }, STAFF, Ct);

        await edit.Should().ThrowAsync<ArgumentException>();
    }

    private async Task<List<FurnitureDefinitionEntity>> DefinitionsAsync()
    {
        await using var dbCtx = _db.CreateDbContext();

        return await dbCtx.FurnitureDefinitions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(Ct);
    }

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

    private static JsonObject Chair(int xdim, string name = "Chair") =>
        new()
        {
            ["id"] = 30,
            ["classname"] = "chair_norja",
            ["revision"] = 61856,
            ["category"] = "chair",
            ["defaultdir"] = 0,
            ["xdim"] = xdim,
            ["ydim"] = 1,
            ["partcolors"] = new JsonObject { ["color"] = new JsonArray("#ffffff", "#F7EBBC") },
            ["name"] = name,
            ["description"] = "Sleek and chic",
            ["adurl"] = "",
            ["offerid"] = 18,
            ["buyout"] = true,
            ["rentofferid"] = -1,
            ["rentbuyout"] = false,
            ["bc"] = true,
            ["excludeddynamic"] = false,
            ["bcofferid"] = 18,
            ["customparams"] = "",
            ["specialtype"] = 1,
            ["canstandon"] = false,
            ["cansiton"] = true,
            ["canlayon"] = false,
            ["canputstuffon"] = false,
            ["height"] = 1.1125,
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
            ["adurl"] = null,
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

    private static HabboFurnitureAssetEntity AssetFile(
        int id,
        string asset,
        int revision,
        int states
    ) =>
        new()
        {
            Id = id,
            AssetName = asset,
            Revision = revision,
            States = states,
        };

    private static FurnitureDefinitionEntity Definition(int spriteId, string name) =>
        new()
        {
            Id = 1,
            SpriteId = spriteId,
            Name = name,
            ProductType = ProductType.Floor,
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
