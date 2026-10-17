using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Gamedata;
using Turbo.Gamedata.Api;
using Turbo.Gamedata.Configuration;
using Turbo.PacketHandlers.Camera;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Rooms;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// The navigator shows a room's thumbnail from <c>navigator.thumbnail.url_base</c> +
/// <c>&lt;room id&gt;.png</c> (<c>RoomEntryElementFactory</c>, <c>RoomInfoPopup</c>,
/// <c>RoomInfoViewCtrl</c>), so the thumbnail the owner took must be loadable at such an address by
/// every player's client: the gamedata host serves what the room thumbnail camera drew.
/// </summary>
public sealed class RoomThumbnailServingTests : IAsyncDisposable
{
    private const int ROOM = 7;

    private readonly string _storage = Path.Combine(
        Path.GetTempPath(),
        "turbo-tests",
        Path.GetRandomFileName()
    );
    private readonly RoomThumbnailStore _thumbnails;
    private readonly GamedataServer _server;
    private readonly HttpClient _http;

    public RoomThumbnailServingTests()
    {
        int port;

        using (var free = new TcpListener(IPAddress.Loopback, 0))
        {
            free.Start();
            port = ((IPEndPoint)free.LocalEndpoint).Port;
        }

        _thumbnails = new RoomThumbnailStore(
            Options.Create(new CameraConfig { StoragePath = _storage })
        );

        var services = new ServiceCollection()
            .AddSingleton<IGamedataFileService>(new NoBuilds())
            .AddSingleton<IRoomThumbnailFiles>(_thumbnails)
            .BuildServiceProvider();

        _server = new GamedataServer(
            services,
            Options.Create(new GamedataConfig { Enabled = true, Url = $"http://127.0.0.1:{port}" }),
            NullLoggerFactory.Instance,
            NullLogger<GamedataServer>.Instance
        );
        _http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _server.StopAsync(CancellationToken.None);
        await _server.DisposeAsync();
    }

    /// <summary>The thumbnail as the camera stores it: the render data, then the renderer's PNG.</summary>
    private async Task<byte[]> TakeThumbnailAsync(byte[] png)
    {
        var (_, pngPath) = await _thumbnails.SaveAsync(
            ROOM,
            "{\"planes\":[]}",
            TestContext.Current.CancellationToken
        );

        await File.WriteAllBytesAsync(pngPath, png, TestContext.Current.CancellationToken);

        return png;
    }

    [Fact]
    public async Task TheThumbnailTheOwnerTook_IsServedToAnyClient()
    {
        var png = await TakeThumbnailAsync([0x89, 0x50, 0x4E, 0x47, 1, 2, 3]);
        await _server.StartAsync(TestContext.Current.CancellationToken);

        using var response = await _http.GetAsync(
            $"/gamedata/navigator-thumbnail/{ROOM}.png",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("*");
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken))
            .Should()
            .Equal(png);
    }

    [Fact]
    public async Task ANewThumbnail_IsShownAtOnce_NotTheCachedOldOne()
    {
        await TakeThumbnailAsync([1]);
        await _server.StartAsync(TestContext.Current.CancellationToken);

        using var first = await _http.GetAsync(
            $"/gamedata/navigator-thumbnail/{ROOM}.png",
            TestContext.Current.CancellationToken
        );

        first.Headers.CacheControl!.NoCache.Should().BeTrue();

        using var unchanged = new HttpRequestMessage(
            HttpMethod.Get,
            $"/gamedata/navigator-thumbnail/{ROOM}.png"
        );
        unchanged.Headers.IfModifiedSince = first.Content.Headers.LastModified;

        using var notModified = await _http.SendAsync(
            unchanged,
            TestContext.Current.CancellationToken
        );

        notModified.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task ARoomWithoutAThumbnail_IsNotFound()
    {
        await _server.StartAsync(TestContext.Current.CancellationToken);

        using var response = await _http.GetAsync(
            "/gamedata/navigator-thumbnail/8.png",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>No gamedata file is asked for here.</summary>
    private sealed class NoBuilds : IGamedataFileService
    {
        public Task<GamedataFileContent> GetCurrentAsync(string file, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<GamedataFileContent?> GetAsync(
            string file,
            string hash,
            CancellationToken ct
        ) => Task.FromResult<GamedataFileContent?>(null);

        public Task<GamedataFileContent> RebuildAsync(string file, CancellationToken ct) =>
            throw new NotSupportedException();

        public void Invalidate(string file) { }
    }
}
