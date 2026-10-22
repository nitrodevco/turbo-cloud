using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Gamedata;
using Turbo.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Files;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.Plugins;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Settings;

/// <summary>
/// Two silos over one database: a setting saved through one reloads that silo's configuration
/// alone, and the other reads the panel's overrides again once it checks the database, without a
/// restart - so an external variable that follows the setting is served by both.
/// </summary>
public sealed class ServerSettingsAcrossSilosTests : IDisposable
{
    private const string APP_SETTINGS = """{ "Test": { "Hotel": { "Name": "Turbo Hotel" } } }""";

    private static readonly PlayerId STAFF = new(7);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly SettingsHarness _settingsA;
    private readonly SettingsHarness _settingsB;
    private readonly ManualTimeProvider _clockB = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ServerSettingsAcrossSilosTests()
    {
        _settingsA = Settings();
        _settingsB = Settings();
    }

    public void Dispose()
    {
        _settingsA.Dispose();
        _settingsB.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task a_setting_saved_on_one_silo_is_served_in_the_others_variables_once_it_checks()
    {
        _db.Insert(
            new GamedataVariableEntity
            {
                Id = 1,
                Key = "hotel.name",
                Value = "\"Own name\"",
                SettingPath = "Test:Hotel:Name",
            }
        );

        var filesA = Files(_settingsA, new ManualTimeProvider());
        var filesB = Files(_settingsB, _clockB);

        (await HotelNameAsync(filesB)).Should().Be("Turbo Hotel");

        await _settingsA.Settings.SaveAsync("Test:Hotel:Name", "\"Habbo Classic\"", STAFF, Ct);

        (await HotelNameAsync(filesA)).Should().Be("Habbo Classic", "the silo it was saved on");

        // Its files' rows are as they were: checking them tells the other silo nothing.
        _clockB.Advance(TimeSpan.FromSeconds(new GamedataConfig().FileCheckSeconds));

        (await HotelNameAsync(filesB)).Should().Be("Turbo Hotel");

        // What the watcher does every Turbo:Settings:CheckSeconds.
        (await _settingsB.Settings.CheckAsync(Ct))
            .Should()
            .BeTrue();

        (await HotelNameAsync(filesB))
            .Should()
            .Be("Habbo Classic", "the other silo read the overrides again");
        _settingsB.Settings.Get("Test:Hotel:Name")!.Value.Should().Be("\"Habbo Classic\"");
    }

    [Fact]
    public async Task a_live_setting_saved_on_one_silo_applies_on_the_other_once_it_checks()
    {
        await _settingsA.Settings.SaveAsync("Test:Hotel:GreetingSeconds", "9", STAFF, Ct);

        _settingsB.Live<TestHotelConfig>().GreetingSeconds.Should().Be(2);

        await _settingsB.Settings.CheckAsync(Ct);

        _settingsB.Live<TestHotelConfig>().GreetingSeconds.Should().Be(9);

        await _settingsA.Settings.ResetAsync("Test:Hotel:GreetingSeconds", STAFF, Ct);
        await _settingsB.Settings.CheckAsync(Ct);

        _settingsB.Live<TestHotelConfig>().GreetingSeconds.Should().Be(2, "put back too");
    }

    [Fact]
    public async Task a_check_that_finds_the_overrides_as_they_were_read_reads_nothing_again()
    {
        await _settingsA.Settings.SaveAsync("Test:Hotel:MaxUsers", "60", STAFF, Ct);

        var version = _settingsA.Settings.Version;

        // The silo it was saved on read them as it saved.
        (await _settingsA.Settings.CheckAsync(Ct))
            .Should()
            .BeFalse();
        _settingsA.Settings.Version.Should().Be(version);
    }

    private SettingsHarness Settings() =>
        new(
            _db,
            APP_SETTINGS,
            (services, configuration) =>
                services.Configure<TestHotelConfig>(
                    configuration.GetSection(TestHotelConfig.SECTION_NAME)
                )
        );

    /// <summary>A silo's gamedata files, built from its own settings, with its own clock.</summary>
    private GamedataFileService Files(SettingsHarness settings, TimeProvider clock) =>
        new(
            _db,
            Options.Create(new GamedataConfig()),
            new FurnitureOfferCatalog(
                _fakes.Create<ICatalogSnapshotProvider<NormalCatalog>>(),
                _fakes.Create<ICatalogSnapshotProvider<BuildersClubCatalog>>()
            ),
            settings.Settings,
            _fakes.Create<IAssetBundleStore>(),
            clock,
            new LoadedPluginGamedata(),
            NullLogger<GamedataFileService>.Instance
        );

    /// <summary>The <c>hotel.name</c> the client would load from the silo's variables now.</summary>
    private static async Task<string> HotelNameAsync(GamedataFileService files)
    {
        var current = await files.GetCurrentAsync(GamedataFiles.EXTERNAL_VARIABLES, Ct);

        return JsonNode
            .Parse(Encoding.UTF8.GetString(GamedataBytes.Decompress(current.Gzipped)))!
            .AsObject()["hotel.name"]!.GetValue<string>();
    }
}
