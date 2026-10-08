using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Furniture;
using Turbo.Furniture.Providers;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Sound.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's song list (<c>/api/songs</c>): staff who may see the catalog list the songs with
/// how many disks carry each; only those who may change the catalog add, edit and remove them,
/// and a refused change says why.
/// </summary>
public sealed class SongEndpointsTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port;
    private readonly IHostedService _server;

    /// <summary>The nodes the staff member holds.</summary>
    private Func<string, bool> _holds = _ => true;

    public SongEndpointsTests()
    {
        using (var free = new TcpListener(IPAddress.Loopback, 0))
        {
            free.Start();
            _port = ((IPEndPoint)free.LocalEndpoint).Port;
        }

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));

        var directory = GrainHarness.Create(
            typeof(FurnitureModule).Assembly,
            "Turbo.Furniture.Grains.SongDirectoryGrain",
            new Fakes(),
            _db
        );
        RoomHarness.SetField(
            directory,
            "_stuffDataFactory",
            new StuffDataFactory(NullLogger<IStuffDataFactory>.Instance)
        );
        ((Grain)directory).OnActivateAsync(CancellationToken.None).GetAwaiter().GetResult();
        _fakes.Instances[(typeof(ISongDirectoryGrain), SingletonGrainId.GLOBAL)] = directory;

        (_server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            _fakes,
            NullLoggerFactory.Instance
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _server.StopAsync(CancellationToken.None);
        _db.Dispose();
    }

    [Fact]
    public async Task A_song_saved_is_listed_with_its_length_and_disks_and_read_with_its_track()
    {
        await _server.StartAsync(Ct);

        var created = await SendAsync(HttpMethod.Post, "/api/songs", Song("Theme", "theme_1"));

        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await JsonAsync(created)).GetProperty("id").GetInt32();

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/songs"));
        var song = list.GetProperty("songs").EnumerateArray().Single();
        song.GetProperty("id").GetInt32().Should().Be(id);
        song.GetProperty("name").GetString().Should().Be("Theme");
        song.GetProperty("author").GetString().Should().Be("Staff");
        song.GetProperty("length").GetInt32().Should().Be(95);
        song.GetProperty("official").GetBoolean().Should().BeTrue();
        song.GetProperty("discs").GetInt32().Should().Be(0);
        song.GetProperty("code").GetString().Should().Be("theme_1");

        var detail = await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/songs/{id}"));
        detail.GetProperty("track").GetString().Should().Be("1:0,4;2:0,4");

        (await SendAsync(HttpMethod.Delete, $"/api/songs/{id}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Get, $"/api/songs/{id}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_song_that_cannot_be_played_is_refused_with_why()
    {
        await _server.StartAsync(Ct);

        var refused = await SendAsync(HttpMethod.Post, "/api/songs", Song("", null));

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(refused)).GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Staff_who_may_only_see_the_catalog_list_songs_but_change_none()
    {
        _holds = node => node != PermissionNodes.Catalog.MANAGE;
        await _server.StartAsync(Ct);

        (await SendAsync(HttpMethod.Get, "/api/songs")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, "/api/songs", Song("Theme", null)))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        _holds = node => node == PermissionNodes.Admin.PANEL;

        (await SendAsync(HttpMethod.Get, "/api/songs"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body = null
    )
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}{path}");

        request.Headers.Authorization = new("Bearer", "a-session");

        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;

    private static object Song(string name, string? code) =>
        new
        {
            name,
            author = "Staff",
            track = "1:0,4;2:0,4",
            length = 95,
            official = true,
            code,
        };
}
