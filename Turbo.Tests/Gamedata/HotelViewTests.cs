using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Files;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.History;
using Turbo.Gamedata.HotelView;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Tests.Catalog;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// The hotel view tool: the reception's <c>landing.view.*</c> variables and the texts its promos
/// show, saved together into the files the client loads, as one change set that rolls back as one.
/// </summary>
public sealed class HotelViewTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);

    private readonly CatalogFixture _catalog = new();
    private readonly SettingsHarness _settings;

    public HotelViewTests() => _settings = new SettingsHarness(_catalog.Db, "{}", (_, _) => { });

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _settings.Dispose();
        _catalog.Dispose();
    }

    [Fact]
    public async Task a_promo_saved_with_its_texts_reaches_the_client_and_rolls_back_as_one()
    {
        var hotel = Hotel();

        await hotel.Variables.SaveAsync(
            "landing.view.dynamic.slot.2.widget",
            "\"bonusrare\"",
            STAFF,
            Ct
        );
        await hotel.Texts.SaveAsync("landing.view.summer.header", "Old header", STAFF, Ct);

        var set = await hotel.View.SaveAsync(
            new HotelViewEdit
            {
                Variables = new Dictionary<string, string?>
                {
                    ["landing.view.dynamic.slot.2.widget"] = "\"widgetcontainer\"",
                    ["landing.view.dynamic.slot.2.conf"] = "\"2026-10-01 10:00,summer\"",
                    ["landing.view.summer.widget"] = "\"generic\"",
                    ["landing.view.summer.conf"] =
                        "\"caption,landing.view.summer.header;catalogbutton,landing.view.summer.button,summer\"",
                },
                Texts = new Dictionary<string, string?>
                {
                    ["landing.view.summer.header"] = "Summer is here",
                    ["landing.view.summer.button"] = "Shop now",
                },
            },
            STAFF,
            Ct
        );

        set.Should().NotBeNull();
        set!.ChangeCount.Should().Be(6);

        var variables = await VariablesAsync(hotel);
        var texts = await TextsAsync(hotel);

        variables["landing.view.dynamic.slot.2.widget"]!
            .GetValue<string>()
            .Should()
            .Be("widgetcontainer");
        variables["landing.view.summer.conf"]!
            .GetValue<string>()
            .Should()
            .StartWith("caption,landing.view.summer.header");
        texts.Should().Contain("landing.view.summer.header=Summer is here");
        texts.Should().Contain("landing.view.summer.button=Shop now");
        (await hotel.View.GetVariablesAsync(Ct))
            .Select(x => x.Key)
            .Should()
            .Contain("landing.view.summer.widget");

        await hotel.History.RollbackAsync(set.Id, STAFF, Ct);

        var rolledBack = await VariablesAsync(hotel);
        var textsBack = await TextsAsync(hotel);

        rolledBack["landing.view.dynamic.slot.2.widget"]!
            .GetValue<string>()
            .Should()
            .Be("bonusrare");
        rolledBack.ContainsKey("landing.view.summer.widget").Should().BeFalse();
        textsBack.Should().Contain("landing.view.summer.header=Old header");
        textsBack.Should().NotContain(x => x.StartsWith("landing.view.summer.button="));
    }

    [Fact]
    public async Task a_save_removes_what_is_set_to_null_and_one_changing_nothing_makes_no_set()
    {
        var hotel = Hotel();

        await hotel.Variables.SaveAsync("landing.view.old.widget", "\"generic\"", STAFF, Ct);

        var removed = await hotel.View.SaveAsync(
            new HotelViewEdit
            {
                Variables = new Dictionary<string, string?>
                {
                    ["landing.view.old.widget"] = null,
                    ["landing.view.never.widget"] = null,
                },
            },
            STAFF,
            Ct
        );

        removed!.ChangeCount.Should().Be(1);
        (await VariablesAsync(hotel)).ContainsKey("landing.view.old.widget").Should().BeFalse();

        var again = await hotel.View.SaveAsync(
            new HotelViewEdit
            {
                Variables = new Dictionary<string, string?> { ["landing.view.old.widget"] = null },
            },
            STAFF,
            Ct
        );

        again.Should().BeNull();
    }

    [Theory]
    [InlineData("socket.url", "\"wss://elsewhere\"")]
    [InlineData("landing.view.", "\"x\"")]
    [InlineData("landing.view.summer.widget", "not json")]
    public async Task a_save_with_anything_it_cant_take_saves_nothing(string key, string value)
    {
        var hotel = Hotel();

        var save = () =>
            hotel.View.SaveAsync(
                new HotelViewEdit
                {
                    Variables = new Dictionary<string, string?>
                    {
                        ["landing.view.common.textcolor"] = "\"ffffff\"",
                        [key] = value,
                    },
                },
                STAFF,
                Ct
            );

        await save.Should().ThrowAsync<ArgumentException>();
        (await hotel.View.GetVariablesAsync(Ct)).Should().BeEmpty();
    }

    private HotelGamedata Hotel()
    {
        var config = Options.Create(new GamedataConfig());
        var writes = new GamedataWriteLock();
        var files = new GamedataFileService(
            _catalog.Db,
            config,
            new FurnitureOfferCatalog(_catalog.NormalProvider(), _catalog.BuildersClubProvider()),
            _settings.Settings,
            NullLogger<GamedataFileService>.Instance
        );
        var hotelTexts = new HotelTextProvider(_catalog.Db, config, TimeProvider.System);

        return new HotelGamedata(
            files,
            new HotelViewService(
                _catalog.Db,
                config,
                files,
                writes,
                _settings.Settings,
                hotelTexts,
                NullLogger<HotelViewService>.Instance
            ),
            new GamedataVariableService(
                _catalog.Db,
                config,
                files,
                writes,
                _settings.Settings,
                NullLogger<GamedataVariableService>.Instance
            ),
            new GamedataTextService(
                _catalog.Db,
                config,
                files,
                writes,
                TimeProvider.System,
                hotelTexts,
                NullLogger<GamedataTextService>.Instance
            ),
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

    /// <summary>The variables file the client would load now.</summary>
    private static async Task<JsonObject> VariablesAsync(HotelGamedata hotel)
    {
        var current = await hotel.Files.GetCurrentAsync(GamedataFiles.EXTERNAL_VARIABLES, Ct);

        return JsonNode
            .Parse(Encoding.UTF8.GetString(GamedataBytes.Decompress(current.Gzipped)))!
            .AsObject();
    }

    /// <summary>The external texts file the client would load now, a line per text.</summary>
    private static async Task<string[]> TextsAsync(HotelGamedata hotel)
    {
        var current = await hotel.Files.GetCurrentAsync(GamedataFiles.EXTERNAL_TEXTS, Ct);

        return Encoding
            .UTF8.GetString(GamedataBytes.Decompress(current.Gzipped))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private sealed record HotelGamedata(
        GamedataFileService Files,
        HotelViewService View,
        GamedataVariableService Variables,
        GamedataTextService Texts,
        GamedataHistoryService History
    );
}
