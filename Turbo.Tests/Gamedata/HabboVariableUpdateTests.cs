using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Files;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.History;
using Turbo.Gamedata.Plugins;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Players;
using Turbo.Tests.Catalog;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// Habbo's external variables as a source of Habbo updates, as its texts are: a check keeps each
/// new version, staff see the keys Habbo added, changed or dropped beside the hotel's, never the
/// ones the hotel owns, take in the ones they pick, and roll a take-in back from the history.
/// </summary>
public sealed class HabboVariableUpdateTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);

    /// <summary>The official client's seasonal switches on 2026-10-21, and some of what surrounds them.</summary>
    private const string HABBO = """
        seasonalcurrency.id.104=letter
        seasonalcurrency.letter.color=dark_mauve
        seasonalcurrency.preset.dark_mauve.border=#813d6a
        seasonalcurrencyindicator.active=101,104
        seasonalcurrencyindicator.currency=101,104
        seasonalcurrencyindicator.page=hween26sc
        seasonalcurrencyindicator.enabled=true
        purse.widget.enabled=1
        group.forums.enabled=true
        roomeditor.limit=60
        badge.tooltip=Habbo's badge
        url.prefix=https://www.habbo.com
        logout.url=${url.prefix}/account/disconnected
        connection.info.host.no=game-no.habbo.com
        link.format.bots.help=https://help.habbo.com/hc/en-us/articles/360011620199-Bots
        furnituredata.url=https://www.habbo.com/gamedata/furnidata_json/0
        landing.view.dynamic.slot.1.conf=caption,landing.view.hween26.header
        renderer.petTypes=dog,cat
        new.user.flow.roomTypes=1,2
        """;

    private readonly CatalogFixture _catalog = new();
    private readonly SettingsHarness _settings;
    private readonly TestOptionsMonitor<GamedataConfig> _config = new(new GamedataConfig());

    public HabboVariableUpdateTests() =>
        _settings = new SettingsHarness(
            _catalog.Db,
            """{ "Test": { "Hotel": { "Name": "Turbo Hotel", "ApiKey": "secret" } } }""",
            (services, configuration) =>
                services.Configure<TestHotelConfig>(
                    configuration.GetSection(TestHotelConfig.SECTION_NAME)
                )
        );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _settings.Dispose();
        _catalog.Dispose();
    }

    [Fact]
    public async Task habbos_changed_switches_wait_and_the_keys_the_hotel_owns_are_never_offered()
    {
        var hotel = Hotel();

        await SummerAsync(hotel);
        await hotel.Variables.LinkAsync(
            "furnituredata.url",
            null,
            GamedataFiles.FURNITURE_DATA,
            STAFF,
            Ct
        );
        await hotel.Variables.SaveAsync("landing.view.dynamic.slot.1.conf", "\"ours\"", STAFF, Ct);
        await hotel.Variables.SaveAsync("renderer.petTypes", "\"dog\"", STAFF, Ct);
        await hotel.Variables.SaveAsync("url.prefix", "\"https://hotel.example\"", STAFF, Ct);
        await hotel.Variables.SaveAsync("new.user.flow.roomTypes", "\"19\"", STAFF, Ct);

        await KeepAsync(hotel, HABBO);

        var preview = await hotel.Habbo.PreviewAsync(null, Ct);

        preview!
            .Items.Select(x => (x.Key, x.Action, x.Current, x.Incoming))
            .Should()
            .BeEquivalentTo(
                new (string, HabboVariableUpdateAction, string?, string?)[]
                {
                    ("badge.tooltip", HabboVariableUpdateAction.Add, null, "\"Habbo's badge\""),
                    ("group.forums.enabled", HabboVariableUpdateAction.Add, null, "true"),
                    (
                        "new.user.flow.roomTypes",
                        HabboVariableUpdateAction.Update,
                        "\"19\"",
                        "\"1,2\""
                    ),
                    (
                        "seasonalcurrency.id.104",
                        HabboVariableUpdateAction.Update,
                        "\"coconuts\"",
                        "\"letter\""
                    ),
                    (
                        "seasonalcurrency.letter.color",
                        HabboVariableUpdateAction.Add,
                        null,
                        "\"dark_mauve\""
                    ),
                    (
                        "seasonalcurrency.preset.dark_mauve.border",
                        HabboVariableUpdateAction.Add,
                        null,
                        "\"#813d6a\""
                    ),
                    (
                        "seasonalcurrencyindicator.active",
                        HabboVariableUpdateAction.Update,
                        "\"101\"",
                        "\"101,104\""
                    ),
                    (
                        "seasonalcurrencyindicator.currency",
                        HabboVariableUpdateAction.Update,
                        "\"101\"",
                        "\"101,104\""
                    ),
                    (
                        "seasonalcurrencyindicator.page",
                        HabboVariableUpdateAction.Update,
                        "\"aug26sc\"",
                        "\"hween26sc\""
                    ),
                },
                options => options.WithStrictOrdering()
            );
        preview.Added.Should().Be(4);
        preview.Updated.Should().Be(5);
        preview.Removed.Should().Be(0);
        // The hotel's flag and number keep their kind: Habbo's same value is no change.
        preview.Unchanged.Should().Be(3);
        // url.prefix, logout.url, the host, the help address, the linked file, the reception's and the pets.
        preview.Protected.Should().Be(7);
    }

    [Fact]
    public async Task a_partial_take_in_changes_only_the_keys_picked_and_rolls_back_as_a_set()
    {
        var hotel = Hotel();

        await SummerAsync(hotel);
        await KeepAsync(hotel, HABBO);

        var version = (await hotel.Habbo.PreviewAsync(null, Ct))!.Version;
        var set = await hotel.Habbo.TakeAsync(
            version.Id,
            ["seasonalcurrency.id.104", "seasonalcurrency.letter.color", "not.waiting"],
            STAFF,
            Ct
        );

        set!.Kind.Should().Be(GamedataChangeKind.Import);
        (await FileAsync(hotel))["seasonalcurrency.id.104"]!
            .GetValue<string>()
            .Should()
            .Be("letter");
        (await FileAsync(hotel))["seasonalcurrency.letter.color"]!
            .GetValue<string>()
            .Should()
            .Be("dark_mauve");
        (await FileAsync(hotel))["seasonalcurrencyindicator.page"]!
            .GetValue<string>()
            .Should()
            .Be("aug26sc");

        var after = await hotel.Habbo.PreviewAsync(null, Ct);

        after!
            .Items.Select(x => x.Key)
            .Should()
            .NotContain(["seasonalcurrency.id.104", "seasonalcurrency.letter.color"])
            .And.Contain("seasonalcurrencyindicator.page");
        after.Version.ImportedAt.Should().NotBeNull();

        var rollback = await hotel.History.RollbackAsync(set.Id, STAFF, Ct);

        rollback!.Skipped.Should().BeEmpty();
        (await FileAsync(hotel))["seasonalcurrency.id.104"]!
            .GetValue<string>()
            .Should()
            .Be("coconuts");
        (await FileAsync(hotel)).Should().NotContainKey("seasonalcurrency.letter.color");
        (await hotel.Habbo.PreviewAsync(null, Ct))!
            .Items.Select(x => x.Key)
            .Should()
            .Contain(["seasonalcurrency.id.104", "seasonalcurrency.letter.color"]);
    }

    [Fact]
    public async Task a_value_the_hotel_changed_after_taking_it_in_stays_its_own_until_habbo_changes_it()
    {
        var hotel = Hotel();

        await KeepAsync(hotel, "seasonalcurrencyindicator.page=hween26sc\nother=1");
        await hotel.Habbo.TakeAsync(
            (await hotel.Habbo.PreviewAsync(null, Ct))!.Version.Id,
            null,
            STAFF,
            Ct
        );
        await hotel.Variables.SaveAsync("seasonalcurrencyindicator.page", "\"ourpage\"", STAFF, Ct);

        var preview = await hotel.Habbo.PreviewAsync(null, Ct);

        preview!.Items.Should().BeEmpty();
        preview.Kept.Should().Be(1);

        await KeepAsync(hotel, "seasonalcurrencyindicator.page=xmas26sc\nother=1");

        (await hotel.Habbo.PreviewAsync(null, Ct))!
            .Items.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    Key = "seasonalcurrencyindicator.page",
                    Action = HabboVariableUpdateAction.Update,
                    Current = "\"ourpage\"",
                    Incoming = "\"xmas26sc\"",
                }
            );
    }

    [Fact]
    public async Task a_key_habbo_drops_is_offered_for_removal_and_its_rollback_brings_it_back()
    {
        var hotel = Hotel();

        await KeepAsync(hotel, "summer.campaign.enabled=true\nother=1");
        await hotel.Habbo.TakeAsync(
            (await hotel.Habbo.PreviewAsync(null, Ct))!.Version.Id,
            null,
            STAFF,
            Ct
        );
        await hotel.Variables.SaveAsync("hotel.only", "1", STAFF, Ct);
        await KeepAsync(hotel, "other=1");

        var preview = await hotel.Habbo.PreviewAsync(null, Ct);

        preview!.Removed.Should().Be(1);
        preview
            .Items.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    Key = "summer.campaign.enabled",
                    Action = HabboVariableUpdateAction.Remove,
                    Current = "true",
                    Incoming = (string?)null,
                }
            );

        var set = await hotel.Habbo.TakeAsync(preview.Version.Id, null, STAFF, Ct);

        (await FileAsync(hotel))
            .Should()
            .NotContainKey("summer.campaign.enabled")
            .And.ContainKey("hotel.only");
        (await hotel.Habbo.PreviewAsync(null, Ct))!.Items.Should().BeEmpty();

        await hotel.History.RollbackAsync(set!.Id, STAFF, Ct);

        (await FileAsync(hotel))["summer.campaign.enabled"]!.GetValue<bool>().Should().BeTrue();
        (await hotel.Habbo.PreviewAsync(null, Ct))!
            .Items.Should()
            .ContainSingle()
            .Which.Key.Should()
            .Be("summer.campaign.enabled");
    }

    [Fact]
    public async Task the_ignore_list_is_read_as_it_changes_and_naming_no_keys_takes_none()
    {
        var hotel = Hotel();

        await KeepAsync(
            hotel,
            "seasonalcurrencyindicator.page=hween26sc\ngroup.forums.enabled=true"
        );

        (await hotel.Habbo.PreviewAsync(null, Ct))!.Items.Should().HaveCount(2);

        _config.Set(new GamedataConfig { HabboVariableIgnore = ["SeasonalCurrency*"] });

        var preview = await hotel.Habbo.PreviewAsync(null, Ct);

        preview!.Items.Should().ContainSingle().Which.Key.Should().Be("group.forums.enabled");
        preview.Protected.Should().Be(1);
        (await hotel.Habbo.TakeAsync(preview.Version.Id, [], STAFF, Ct)).Should().BeNull();
        (
            await hotel.Habbo.TakeAsync(
                preview.Version.Id,
                ["seasonalcurrencyindicator.page"],
                STAFF,
                Ct
            )
        )
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task a_check_keeps_a_version_only_when_habbos_file_changed_read_where_the_setting_says()
    {
        var host = new Host(Encoding.UTF8.GetBytes("a=1\nb=2\n"));
        var client = new HabboGamedataClient(
            host,
            NullLogger<HabboGamedataClient>.Instance,
            Options.Create(
                new GamedataConfig { HabboGamedataUrl = "http://127.0.0.1:1/{domain}/{file}" }
            )
        );
        var releases = Releases(client);

        var file = await client.GetExternalVariablesFileAsync("com", Ct);
        var first = await releases.KeepVariablesAsync("com", file, 2, DateTime.UtcNow, Ct);
        var again = await releases.KeepVariablesAsync("com", file, 2, DateTime.UtcNow, Ct);

        host.Asked.Should().Equal("http://127.0.0.1:1/com/external_variables");
        first.IsNew.Should().BeTrue();
        first.Version.VariableCount.Should().Be(2);
        again.IsNew.Should().BeFalse();
        again.Version.Id.Should().Be(first.Version.Id);
        (await releases.GetLatestVariablesAsync(Ct))!.Id.Should().Be(first.Version.Id);
    }

    /// <summary>The hotel as nitrodev.co was on 2026-10-21: the summer's seasonal currency.</summary>
    private static async Task SummerAsync(HotelGamedata hotel)
    {
        await hotel.Variables.SaveAsync("seasonalcurrency.id.104", "\"coconuts\"", STAFF, Ct);
        await hotel.Variables.SaveAsync("seasonalcurrencyindicator.active", "\"101\"", STAFF, Ct);
        await hotel.Variables.SaveAsync("seasonalcurrencyindicator.currency", "\"101\"", STAFF, Ct);
        await hotel.Variables.SaveAsync("seasonalcurrencyindicator.page", "\"aug26sc\"", STAFF, Ct);
        await hotel.Variables.SaveAsync("seasonalcurrencyindicator.enabled", "true", STAFF, Ct);
        await hotel.Variables.SaveAsync("purse.widget.enabled", "1", STAFF, Ct);
        await hotel.Variables.SaveAsync("roomeditor.limit", "60", STAFF, Ct);
    }

    private async Task KeepAsync(HotelGamedata hotel, string variables)
    {
        var file = Encoding.UTF8.GetBytes(variables);

        await hotel.Releases.KeepVariablesAsync(
            "com",
            file,
            HabboGamedataClient.ParseVariables(file).Count,
            DateTime.UtcNow,
            Ct
        );
    }

    /// <summary>The variables file the client would load now.</summary>
    private static async Task<JsonObject> FileAsync(HotelGamedata hotel)
    {
        var current = await hotel.Files.GetCurrentAsync(GamedataFiles.EXTERNAL_VARIABLES, Ct);

        return JsonNode
            .Parse(Encoding.UTF8.GetString(GamedataBytes.Decompress(current.Gzipped)))!
            .AsObject();
    }

    private HabboReleaseService Releases(HabboGamedataClient client) =>
        new(
            _catalog.Db,
            Options.Create(new GamedataConfig()),
            client,
            TimeProvider.System,
            NullLogger<HabboReleaseService>.Instance
        );

    private HotelGamedata Hotel()
    {
        var config = Options.Create(new GamedataConfig());
        var writes = new GamedataWriteLock();
        var files = new GamedataFileService(
            _catalog.Db,
            config,
            new FurnitureOfferCatalog(_catalog.NormalProvider(), _catalog.BuildersClubProvider()),
            _settings.Settings,
            new Fakes().Create<IAssetBundleStore>(),
            TimeProvider.System,
            new LoadedPluginGamedata(),
            NullLogger<GamedataFileService>.Instance
        );
        var hotelTexts = new HotelTextProvider(_catalog.Db, config, TimeProvider.System);
        var client = new HabboGamedataClient(
            new Fakes().Create<IHttpClientFactory>(),
            NullLogger<HabboGamedataClient>.Instance
        );

        return new HotelGamedata(
            files,
            new GamedataVariableService(
                _catalog.Db,
                config,
                files,
                writes,
                _settings.Settings,
                client,
                NullLogger<GamedataVariableService>.Instance
            ),
            new HabboVariableService(
                _catalog.Db,
                _config,
                files,
                writes,
                TimeProvider.System,
                NullLogger<HabboVariableService>.Instance
            ),
            Releases(client),
            new GamedataHistoryService(
                _catalog.Db,
                config,
                _catalog.Definitions,
                files,
                writes,
                hotelTexts,
                _catalog.Fakes.Create<IFigureDataProvider>(),
                NullLogger<GamedataHistoryService>.Instance
            )
        );
    }

    private sealed record HotelGamedata(
        GamedataFileService Files,
        GamedataVariableService Variables,
        HabboVariableService Habbo,
        HabboReleaseService Releases,
        GamedataHistoryService History
    );

    /// <summary>Habbo's gamedata host, serving one file and noting what it was asked for.</summary>
    private sealed class Host(byte[] file) : HttpMessageHandler, IHttpClientFactory
    {
        public List<string> Asked { get; } = [];

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Asked.Add(request.RequestUri!.ToString());

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(file) }
            );
        }
    }
}
