using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Guilds;
using Turbo.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Files;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.Plugins;
using Turbo.Gamedata.Products;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Settings;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// Two silos over one database: an edit made through one is told to that silo's files alone, and
/// the other builds its own files again once it checks the database, without a restart.
/// </summary>
public sealed class GamedataFilesAcrossSilosTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);

    /// <summary>How often a silo checks its files' rows in the database, by default.</summary>
    private const int CHECK_SECONDS = 5;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly GamedataConfig _config = new();
    private readonly ManualTimeProvider _clockA = new();
    private readonly ManualTimeProvider _clockB = new();
    private readonly GamedataFileService _siloA;
    private readonly GamedataFileService _siloB;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GamedataFilesAcrossSilosTests()
    {
        _siloA = Silo(_clockA);
        _siloB = Silo(_clockB);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task a_product_edited_on_one_silo_is_served_by_the_other_once_it_checks()
    {
        var productsA = new GamedataProductService(
            _db,
            Options.Create(_config),
            _siloA,
            new GamedataWriteLock(),
            _clockA,
            NullLogger<GamedataProductService>.Instance
        );

        await productsA.SaveAsync("a_sofa", "Sofa", "Sits two", STAFF, Ct);

        var before = await _siloB.GetCurrentAsync(GamedataFiles.PRODUCT_DATA, Ct);

        // Only the name changes: no row is added, so only the edit's change set says so.
        await productsA.SaveAsync("a_sofa", "Grand sofa", "Sits two", STAFF, Ct);

        var edited = await _siloA.GetCurrentAsync(GamedataFiles.PRODUCT_DATA, Ct);

        edited.File.Hash.Should().NotBe(before.File.Hash, "the silo the edit was made on knows");

        _clockB.Advance(TimeSpan.FromSeconds(CHECK_SECONDS));

        var served = await _siloB.GetCurrentAsync(GamedataFiles.PRODUCT_DATA, Ct);

        served.File.Hash.Should().Be(edited.File.Hash, "the other silo checked the database");
        System
            .Text.Encoding.UTF8.GetString(GamedataBytes.Decompress(served.Gzipped))
            .Should()
            .Contain("Grand sofa");
    }

    [Fact]
    public async Task a_badge_colour_changed_on_one_silo_is_served_by_the_other_once_it_checks()
    {
        _db.Insert(
            new GuildColorEntity
            {
                Id = 1,
                Slot = GuildColorSlotType.Badge,
                ColorId = 1,
                Color = "ffffff",
            }
        );

        var before = await _siloB.GetCurrentAsync(GamedataFiles.BADGE_PARTS, Ct);

        // As the panel's guild editor does: the row saved, then this silo's file told.
        await using (var dbCtx = await _db.CreateDbContextAsync(Ct))
        {
            dbCtx.GuildColors.Single(x => x.Id == 1).Color = "ff8800";
            await dbCtx.SaveChangesAsync(Ct);
        }

        _siloA.Invalidate(GamedataFiles.BADGE_PARTS);

        var edited = await _siloA.GetCurrentAsync(GamedataFiles.BADGE_PARTS, Ct);

        edited.File.Hash.Should().NotBe(before.File.Hash);

        _clockB.Advance(TimeSpan.FromSeconds(CHECK_SECONDS));

        (await _siloB.GetCurrentAsync(GamedataFiles.BADGE_PARTS, Ct))
            .File.Hash.Should()
            .Be(edited.File.Hash);
    }

    /// <summary>A silo's file service, as its host builds it, with the silo's own clock.</summary>
    private GamedataFileService Silo(TimeProvider clock) =>
        ActivatorUtilities.CreateInstance<GamedataFileService>(
            new ServiceCollection().AddSingleton(clock).BuildServiceProvider(),
            _db,
            Options.Create(_config),
            new FurnitureOfferCatalog(
                _fakes.Create<ICatalogSnapshotProvider<NormalCatalog>>(),
                _fakes.Create<ICatalogSnapshotProvider<BuildersClubCatalog>>()
            ),
            _fakes.Create<IServerSettings>(),
            _fakes.Create<IAssetBundleStore>(),
            new LoadedPluginGamedata(),
            NullLogger<GamedataFileService>.Instance
        );
}
