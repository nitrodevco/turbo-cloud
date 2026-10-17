using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Gamedata.Api;
using Turbo.Gamedata.Configuration;
using Turbo.PacketHandlers.Camera;
using Turbo.Primitives.Camera;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// The photo dialog loads <c>stories.image_url_base</c> + the photo's url
/// (<c>PhotoPurchaseConfirmationDialog.setImageUrl</c>) and a photo poster the same with
/// <c>_small</c> (<c>FurnitureExternalImageVisualization</c>), build WIN63-202610151745. The
/// renderer drew the photos to disk but nothing served them, so no client could load one: the
/// gamedata host serves them under <c>/gamedata/</c>, where the photo's url goes.
/// </summary>
public sealed class CameraPhotoServingTests : IAsyncDisposable
{
    private readonly string _storage = Path.Combine(
        Path.GetTempPath(),
        "turbo-tests",
        Path.GetRandomFileName()
    );
    private readonly CameraPhotoStore _store;
    private readonly GamedataServer _server;
    private readonly HttpClient _http;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CameraPhotoServingTests()
    {
        int port;

        using (var free = new TcpListener(IPAddress.Loopback, 0))
        {
            free.Start();
            port = ((IPEndPoint)free.LocalEndpoint).Port;
        }

        var config = Options.Create(new CameraConfig { StoragePath = _storage });

        _store = new CameraPhotoStore(config);

        var services = new ServiceCollection()
            .AddSingleton<IGamedataFileService>(new NoBuilds())
            .AddSingleton<ICameraPhotoFiles>(new CameraPhotoFiles(config))
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

    [Fact]
    public async Task A_photo_and_its_poster_copy_are_served_at_the_url_the_client_is_sent()
    {
        var photo = await _store.SavePhotoAsync(1, "{\"planes\":[]}", Ct);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 1];
        byte[] small = [0x89, 0x50, 0x4E, 0x47, 2];

        await File.WriteAllBytesAsync(photo.PngPath, png, Ct);
        await File.WriteAllBytesAsync(photo.PngPath.Replace(".png", "_small.png"), small, Ct);
        await _server.StartAsync(Ct);

        using var full = await _http.GetAsync("/gamedata/" + photo.Url, Ct);
        using var poster = await _http.GetAsync(
            "/gamedata/" + photo.Url.Replace(".png", "_small.png"),
            Ct
        );

        full.StatusCode.Should().Be(HttpStatusCode.OK);
        full.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        full.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("*");
        (await full.Content.ReadAsByteArrayAsync(Ct)).Should().Equal(png);
        (await poster.Content.ReadAsByteArrayAsync(Ct)).Should().Equal(small);
    }

    /// <summary>
    /// A poster's state is only the photo's id; its viewer loads <c>extra_data_service_url</c> + the
    /// id (<c>ExternalImageWidget.loadExternalData</c>) and posters on the wall POST their ids
    /// together (<c>ExtraDataManager.handleBatch</c>, a JSON array; the answer's <c>id</c> and
    /// <c>url</c> per poster), and nothing answered either.
    /// </summary>
    [Fact]
    public async Task A_bought_photos_extra_data_is_served_by_its_id_alone_and_in_a_batch()
    {
        var photo = await _store.SavePhotoAsync(1, "{\"planes\":[]}", Ct);
        var extraData = CameraPhotoPoster.ExtraDataOf(photo, 1, "agent1");

        await _store.SaveExtraDataAsync(photo, extraData, Ct);
        await _server.StartAsync(Ct);

        using var one = await _http.GetAsync("/gamedata/photodata/" + photo.Id, Ct);

        one.StatusCode.Should().Be(HttpStatusCode.OK);
        one.Content.Headers.ContentType!.ToString().Should().Be("application/json; charset=utf-8");
        one.Headers.CacheControl!.NoStore.Should().BeTrue();
        one.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("*");
        (await one.Content.ReadAsStringAsync(Ct)).Should().Be(extraData);

        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/gamedata/photodata/");

        preflight.Headers.Add("Origin", "https://nitrodev.co");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var allowed = await _http.SendAsync(preflight, Ct);

        allowed
            .Headers.GetValues("Access-Control-Allow-Methods")
            .Should()
            .Contain(x => x.Contains("POST"));

        using var batch = await _http.PostAsync(
            "/gamedata/photodata/",
            new StringContent(
                $"[\"{photo.Id}\",\"0123456789abcdef0123456789abcdef\"]",
                System.Text.Encoding.UTF8,
                "application/json"
            ),
            Ct
        );

        batch.StatusCode.Should().Be(HttpStatusCode.OK);
        (await batch.Content.ReadAsStringAsync(Ct))
            .Should()
            .Be($"[{{\"id\":\"{photo.Id}\",\"url\":\"{photo.Url}\"}}]");
    }

    [Theory]
    [InlineData("/gamedata/photodata/0123456789abcdef0123456789abcdef")]
    [InlineData("/gamedata/photodata/..%2F..%2Fappsettings")]
    [InlineData("/gamedata/photos/0123456789abcdef0123456789abcdef.png")]
    [InlineData("/gamedata/photos/..%2F..%2Fappsettings.png")]
    [InlineData("/gamedata/photos/thumbnails.png")]
    public async Task Anything_but_a_drawn_photo_is_not_found(string path)
    {
        await _server.StartAsync(Ct);

        using var response = await _http.GetAsync(path, Ct);

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
