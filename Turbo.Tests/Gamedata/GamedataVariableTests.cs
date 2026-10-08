using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Gamedata;
using Turbo.Gamedata;
using Turbo.Gamedata.Api;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Files;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.History;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;
using Turbo.Tests.Catalog;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// The client's configuration kept by the hotel as its external variables: edited and imported by
/// staff, rolled back as a set, and served from the gamedata host by hash like every other
/// gamedata file - with the hotel's own gamedata addresses written into it by hash.
/// </summary>
public sealed class GamedataVariableTests : IAsyncDisposable
{
    private const string PUBLIC_URL = "https://hotel.example";

    private static readonly PlayerId STAFF = new(7);

    private readonly CatalogFixture _catalog = new();
    private readonly List<GamedataServer> _servers = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        foreach (var server in _servers)
        {
            await server.StopAsync(CancellationToken.None);
            await server.DisposeAsync();
        }

        _catalog.Dispose();
    }

    [Fact]
    public async Task a_saved_variable_is_served_by_hash_and_a_change_moves_the_hash()
    {
        var hotel = Hotel();
        var http = await StartAsync(hotel);

        await hotel.Variables.SaveAsync("socket.url", "\"wss://hotel.example/ws\"", STAFF, Ct);
        await hotel.Variables.SaveAsync("catalog.deep.hierarchy", "true", STAFF, Ct);
        await hotel.Variables.SaveAsync("pet.types", "[\"dog\", \"cat\"]", STAFF, Ct);

        var first = await CurrentAddressAsync(http);
        var config = await ReadAsync(http, first);

        config["socket.url"]!.GetValue<string>().Should().Be("wss://hotel.example/ws");
        config["catalog.deep.hierarchy"]!.GetValue<bool>().Should().BeTrue();
        config["pet.types"]!
            .AsArray()
            .Select(x => x!.GetValue<string>())
            .Should()
            .Equal("dog", "cat");

        var hashes = JsonNode.Parse(await http.GetStringAsync("/gamedata/hashes", Ct))!["hashes"]!;

        hashes
            .AsArray()
            .Should()
            .ContainSingle(x => x!["name"]!.GetValue<string>() == "external_variables")
            .Which!["hash"]!
            .GetValue<string>()
            .Should()
            .Be(first.Split('/')[^1]);

        await hotel.Variables.SaveAsync("socket.url", "\"wss://other.example/ws\"", STAFF, Ct);

        var second = await CurrentAddressAsync(http);

        second.Should().NotBe(first);
        (await ReadAsync(http, second))["socket.url"]!
            .GetValue<string>()
            .Should()
            .Be("wss://other.example/ws");
        // The build before is kept for clients that loaded its address.
        (await ReadAsync(http, first))["socket.url"]!
            .GetValue<string>()
            .Should()
            .Be("wss://hotel.example/ws");
    }

    [Fact]
    public async Task the_hotels_gamedata_addresses_are_written_by_hash_and_follow_a_rebuild()
    {
        var hotel = Hotel(PUBLIC_URL);

        // Kept from before the hotel had a public address; the hotel's own address wins.
        _catalog.Db.Insert(
            new GamedataVariableEntity
            {
                Key = "gamedata.urls.externalTexts",
                Value = "\"https://old\"",
            }
        );
        await hotel.Texts.SaveAsync("hello", "Hello", STAFF, Ct);

        var before = await VariablesAsync(hotel);
        var texts = await hotel.Files.GetCurrentAsync(GamedataFiles.EXTERNAL_TEXTS, Ct);
        var furniture = await hotel.Files.GetCurrentAsync(GamedataFiles.FURNITURE_DATA, Ct);

        before.Variables["furnituredata.url"]!
            .GetValue<string>()
            .Should()
            .Be($"{PUBLIC_URL}/gamedata/furnidata_json/{furniture.File.Hash}");
        before.Variables["gamedata.urls.externalTexts"]!
            .GetValue<string>()
            .Should()
            .Be($"{PUBLIC_URL}/gamedata/external_flash_texts/{texts.File.Hash}");
        before.Variables.Select(x => x.Key).Should().Contain(["productdata.url", "figuredata.url"]);

        await hotel.Texts.SaveAsync("hello", "Hello again", STAFF, Ct);

        var after = await VariablesAsync(hotel);
        var newTexts = await hotel.Files.GetCurrentAsync(GamedataFiles.EXTERNAL_TEXTS, Ct);

        newTexts.File.Hash.Should().NotBe(texts.File.Hash);
        after.Hash.Should().NotBe(before.Hash);
        after.Variables["gamedata.urls.externalTexts"]!
            .GetValue<string>()
            .Should()
            .Be($"{PUBLIC_URL}/gamedata/external_flash_texts/{newTexts.File.Hash}");
    }

    [Fact]
    public async Task an_address_the_hotel_writes_cant_be_set_by_staff_or_an_import()
    {
        var hotel = Hotel(PUBLIC_URL);

        var save = () => hotel.Variables.SaveAsync("furnituredata.url", "\"https://x\"", STAFF, Ct);

        await save.Should().ThrowAsync<ArgumentException>();

        var preview = await hotel.Variables.PreviewImportAsync(
            """{ "furnituredata.url": "https://x", "socket.url": "wss://y" }""",
            Ct
        );

        preview.Skipped.Should().Equal("furnituredata.url");
        preview.Added.Should().Be(1);
        (await hotel.Variables.SearchAsync(null, 0, Ct))
            .Stamped.Select(x => x.Key)
            .Should()
            .BeEquivalentTo(
                "furnituredata.url",
                "productdata.url",
                "gamedata.urls.externalTexts",
                "figuredata.url"
            );
    }

    [Fact]
    public async Task without_a_public_address_the_hotel_writes_no_addresses()
    {
        var hotel = Hotel();

        await hotel.Variables.SaveAsync("furnituredata.url", "\"https://cdn/furni\"", STAFF, Ct);

        (await VariablesAsync(hotel)).Variables["furnituredata.url"]!
            .GetValue<string>()
            .Should()
            .Be("https://cdn/furni");
        (await hotel.Variables.SearchAsync(null, 0, Ct)).Stamped.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "true")]
    [InlineData("socket.url", "wss://not-quoted")]
    [InlineData("socket.url", "")]
    public async Task a_variable_the_file_cant_hold_is_refused(string key, string value)
    {
        var save = () => Hotel().Variables.SaveAsync(key, value, STAFF, Ct);

        await save.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task an_import_adds_and_changes_keys_keeps_the_rest_and_rolls_back_as_a_set()
    {
        var hotel = Hotel();

        await hotel.Variables.SaveAsync("kept", "1", STAFF, Ct);
        await hotel.Variables.SaveAsync("changed", "1", STAFF, Ct);
        await hotel.Variables.SaveAsync("same", "\"x\"", STAFF, Ct);

        const string CONFIG = """
            {
                "changed": 2,
                "same": "x",
                "added": [1, 2],
                "twice": "first",
                "twice": "last",
            }
            """;

        var preview = await hotel.Variables.PreviewImportAsync(CONFIG, Ct);

        preview.Added.Should().Be(2);
        preview.Updated.Should().Be(1);
        preview.Unchanged.Should().Be(1);

        var set = await hotel.Variables.ImportAsync(CONFIG, STAFF, Ct);

        (await VariablesAsync(hotel))
            .Variables.ToDictionary(x => x.Key, x => x.Value!.ToJsonString())
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["kept"] = "1",
                    ["changed"] = "2",
                    ["same"] = "\"x\"",
                    ["added"] = "[1,2]",
                    ["twice"] = "\"last\"",
                }
            );
        (await hotel.Variables.ImportAsync(CONFIG, STAFF, Ct)).Should().BeNull();

        await hotel.History.RollbackAsync(set!.Id, STAFF, Ct);

        (await VariablesAsync(hotel))
            .Variables.ToDictionary(x => x.Key, x => x.Value!.ToJsonString())
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["kept"] = "1",
                    ["changed"] = "1",
                    ["same"] = "\"x\"",
                }
            );
    }

    [Fact]
    public async Task a_removed_variable_comes_back_when_its_removal_is_rolled_back()
    {
        var hotel = Hotel();

        await hotel.Variables.SaveAsync("socket.url", "\"wss://x\"", STAFF, Ct);
        (await hotel.Variables.DeleteAsync("socket.url", STAFF, Ct)).Should().BeTrue();

        (await VariablesAsync(hotel)).Variables.Should().BeEmpty();

        var removal = (await hotel.History.ListAsync(0, Ct))[0];

        await hotel.History.RollbackAsync(removal.Id, STAFF, Ct);

        (await VariablesAsync(hotel)).Variables["socket.url"]!
            .GetValue<string>()
            .Should()
            .Be("wss://x");
    }

    [Fact]
    public async Task a_config_that_isnt_a_json_object_is_refused()
    {
        var hotel = Hotel();

        var notJson = () => hotel.Variables.PreviewImportAsync("socket.url=wss://x", Ct);
        var notObject = () => hotel.Variables.ImportAsync("[1, 2]", STAFF, Ct);

        await notJson.Should().ThrowAsync<ArgumentException>();
        await notObject.Should().ThrowAsync<ArgumentException>();
    }

    private HotelGamedata Hotel(string publicUrl = "")
    {
        var config = Options.Create(new GamedataConfig { PublicUrl = publicUrl });
        var writes = new GamedataWriteLock();
        var files = new GamedataFileService(
            _catalog.Db,
            config,
            new FurnitureOfferCatalog(_catalog.NormalProvider(), _catalog.BuildersClubProvider()),
            NullLogger<GamedataFileService>.Instance
        );
        var hotelTexts = new HotelTextProvider(_catalog.Db, config, TimeProvider.System);

        return new HotelGamedata(
            files,
            new GamedataVariableService(
                _catalog.Db,
                config,
                files,
                writes,
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

    /// <summary>The variables file the client would load now, read as it reads it.</summary>
    private static async Task<(string Hash, JsonObject Variables)> VariablesAsync(
        HotelGamedata hotel
    )
    {
        var current = await hotel.Files.GetCurrentAsync(GamedataFiles.EXTERNAL_VARIABLES, Ct);

        return (
            current.File.Hash,
            JsonNode
                .Parse(Encoding.UTF8.GetString(GamedataBytes.Decompress(current.Gzipped)))!
                .AsObject()
        );
    }

    /// <summary>The gamedata host serving the hotel's files, and a client of it that sees redirects.</summary>
    private async Task<HttpClient> StartAsync(HotelGamedata hotel)
    {
        int port;

        using (var free = new TcpListener(IPAddress.Loopback, 0))
        {
            free.Start();
            port = ((IPEndPoint)free.LocalEndpoint).Port;
        }

        var server = new GamedataServer(
            new ServiceCollection()
                .AddSingleton<IGamedataFileService>(hotel.Files)
                .BuildServiceProvider(),
            Options.Create(new GamedataConfig { Enabled = true, Url = $"http://127.0.0.1:{port}" }),
            NullLoggerFactory.Instance,
            NullLogger<GamedataServer>.Instance
        );

        _servers.Add(server);
        await server.StartAsync(Ct);

        return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}"),
        };
    }

    /// <summary>Where the address the client is configured with, <c>/0</c>, sends it now.</summary>
    private static async Task<string> CurrentAddressAsync(HttpClient http)
    {
        using var response = await http.GetAsync("/gamedata/external_variables/0", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.TemporaryRedirect);

        return response.Headers.Location!.ToString();
    }

    private static async Task<JsonObject> ReadAsync(HttpClient http, string address)
    {
        using var response = await http.GetAsync(address, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        return JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct))!.AsObject();
    }

    private sealed record HotelGamedata(
        GamedataFileService Files,
        GamedataVariableService Variables,
        GamedataTextService Texts,
        GamedataHistoryService History
    );
}
