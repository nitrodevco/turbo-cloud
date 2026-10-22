using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Assets.Conversion;
using Turbo.Catalog.Providers;
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
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Texts;
using Turbo.Tests.Assets;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// A definition made from an uploaded furniture bundle: the bundle's own data prefills it (its
/// size, height, states, and the server logic its client logic names), staff change what they
/// like and make it - with a <c>classname*N</c> for each colour, under sprite ids no other
/// definition has - as one change set that rolls back as a whole.
/// </summary>
public sealed class FurnitureFromBundleTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);

    private const string CHEST = "old_chest";

    // The logics the server registers: the chest's furniture_dice is the server's dice.
    private static readonly IReadOnlyList<string> LOGICS =
    [
        "default_floor",
        "default_wall",
        "dice",
    ];

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly AssetFolder _folder;
    private readonly GamedataFurnitureService _furniture;
    private readonly GamedataHistoryService _history;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public FurnitureFromBundleTests()
    {
        var config = Options.Create(new GamedataConfig());
        var definitions = _fakes.Create<IFurnitureDefinitionProvider>();
        var writes = new GamedataWriteLock();
        var files = _fakes.Create<IGamedataFileService>();
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

        _folder = new AssetFolder(_db);
        _fakes.Handlers["get_" + nameof(IRoomObjectLogicProvider.LogicTypes)] = _ => LOGICS;
        _furniture = new GamedataFurnitureService(
            _db,
            config,
            definitions,
            files,
            offers,
            habboItems,
            furnitureFiles,
            new FurnitureStatusIndex(_db, habboItems, furnitureFiles, offers),
            writes,
            _fakes.Create<IRoomObjectLogicProvider>(),
            _folder.Store,
            TimeProvider.System,
            NullLogger<GamedataFurnitureService>.Instance
        );
        _history = new GamedataHistoryService(
            _db,
            config,
            definitions,
            files,
            writes,
            _fakes.Create<IHotelTextProvider>(),
            _fakes.Create<Turbo.Primitives.Figures.IFigureDataProvider>(),
            NullLogger<GamedataHistoryService>.Instance
        );

        // A definition of the hotel's own already holds sprite id 500.
        var chair = FurnitureRecords.Blank(ProductType.Floor, 500, "chair", "default_floor");

        chair.Id = 1;
        _db.Insert(chair);
    }

    public void Dispose()
    {
        _folder.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task A_bundle_of_the_old_layout_prefills_a_definition_from_its_own_data()
    {
        await KeepBundleAsync();

        var draft = await _furniture.DraftFromBundleAsync(CHEST, Ct);

        draft!.ProductType.Should().Be(ProductType.Floor);
        draft.Colors.Should().Equal(1, 2);
        draft.Existing.Should().BeEmpty();
        JsonNode
            .DeepEquals(
                JsonNode.Parse(draft.Fields),
                JsonNode.Parse(
                    """
                    {
                        "classname": "old_chest", "logic": "dice", "name": "old_chest",
                        "description": "", "states": 2, "xdim": 2, "ydim": 1, "height": 0.5
                    }
                    """
                )
            )
            .Should()
            .BeTrue(draft.Fields);
        JsonDocument
            .Parse(draft.File)
            .RootElement.GetProperty("directions")
            .EnumerateArray()
            .Select(x => x.GetInt32())
            .Should()
            .Equal(90, 180);

        (await _furniture.DraftFromBundleAsync("no_such_bundle", Ct)).Should().BeNull();
        (await _furniture.DraftFromBundleAsync("../escape", Ct)).Should().BeNull();
    }

    [Fact]
    public async Task A_definition_and_its_colours_are_made_under_free_sprite_ids_and_roll_back_whole()
    {
        await KeepBundleAsync();

        var draft = await _furniture.DraftFromBundleAsync(CHEST, Ct);
        var fields = JsonNode.Parse(draft!.Fields)!.AsObject();

        // Staff say what the data can't: it is a surface, and it has a name.
        fields["canputstuffon"] = true;
        fields["name"] = "Old chest";

        var ids = await _furniture.CreateDefinitionsAsync(
            draft.ProductType,
            fields,
            draft.Colors,
            STAFF,
            Ct
        );

        ids.Should().HaveCount(3);

        await using (var ctx = await _db.CreateDbContextAsync(Ct))
        {
            var made = await ctx
                .FurnitureDefinitions.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .OrderBy(x => x.SpriteId)
                .ToListAsync(Ct);

            made.Select(x => (x.Name, x.SpriteId))
                .Should()
                .Equal(("old_chest", 501), ("old_chest*1", 502), ("old_chest*2", 503));
            made.Should()
                .AllSatisfy(x =>
                {
                    x.Logic.Should().Be("dice");
                    x.PublicName.Should().Be("Old chest");
                    x.Width.Should().Be(2);
                    x.Length.Should().Be(1);
                    x.StackHeight.Should().Be(0.5);
                    x.TotalStates.Should().Be(2);
                    x.CanStack.Should().BeTrue();
                    x.CanSit.Should().BeFalse();
                });
        }

        (await _furniture.DraftFromBundleAsync(CHEST, Ct))!
            .Existing.Select(x => x.ClassName)
            .Should()
            .Equal("old_chest", "old_chest*1", "old_chest*2");

        // The same classname again is refused, and nothing is made.
        var again = () =>
            _furniture.CreateDefinitionsAsync(ProductType.Floor, fields, [], STAFF, Ct);

        await again.Should().ThrowAsync<ArgumentException>().WithMessage("*old_chest*");

        // One change set, which rolls back as a whole.
        int setId;

        await using (var ctx = await _db.CreateDbContextAsync(Ct))
        {
            var set = await ctx
                .GamedataChangeSets.AsNoTracking()
                .Include(x => x.Changes)
                .SingleAsync(Ct);

            set.Kind.Should().Be(GamedataChangeKind.Edit);
            set.Changes!.Should().HaveCount(3).And.OnlyContain(x => x.Before == null);
            setId = set.Id;
        }

        await _history.RollbackAsync(setId, STAFF, Ct);

        await using (var ctx = await _db.CreateDbContextAsync(Ct))
            (await ctx.FurnitureDefinitions.Select(x => x.Name).ToListAsync(Ct))
                .Should()
                .Equal("chair");
    }

    [Fact]
    public async Task A_definition_is_refused_without_a_classname_or_with_a_field_of_the_wrong_kind()
    {
        var noName = () =>
            _furniture.CreateDefinitionsAsync(
                ProductType.Floor,
                new JsonObject { ["name"] = "Nameless" },
                [],
                STAFF,
                Ct
            );
        var wallSize = () =>
            _furniture.CreateDefinitionsAsync(
                ProductType.Wall,
                new JsonObject { ["classname"] = "poster_x", ["xdim"] = 2 },
                [],
                STAFF,
                Ct
            );
        var takenSprite = () =>
            _furniture.CreateDefinitionsAsync(
                ProductType.Floor,
                new JsonObject { ["classname"] = "stool", ["id"] = 500 },
                [],
                STAFF,
                Ct
            );
        var variantName = () =>
            _furniture.CreateDefinitionsAsync(
                ProductType.Floor,
                new JsonObject { ["classname"] = "stool*2" },
                [],
                STAFF,
                Ct
            );

        await noName.Should().ThrowAsync<ArgumentException>().WithMessage("*classname*");
        await wallSize.Should().ThrowAsync<ArgumentException>().WithMessage("xdim*");
        await takenSprite.Should().ThrowAsync<ArgumentException>().WithMessage("*500*");
        await variantName.Should().ThrowAsync<ArgumentException>().WithMessage("*stool*2*N*");

        await using var ctx = await _db.CreateDbContextAsync(Ct);

        (await ctx.FurnitureDefinitions.CountAsync(Ct)).Should().Be(1);
    }

    /// <summary>The old bundle kept as an upload keeps it: repacked as the current layout.</summary>
    private Task KeepBundleAsync() =>
        _folder.Store.WriteAsync(
            AssetBundleKind.Furniture,
            CHEST,
            NitroConverter.Convert(LegacyNitroBundleTests.Legacy(CHEST), CHEST).Write(),
            Ct
        );
}
