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
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// The client loads the files from its own subdomain, so every response must let any site read
/// it. A build is cached for good, so a copy the browser first loaded without an <c>Origin</c>
/// (opened in a tab) is the one the client's fetch later gets.
/// </summary>
public sealed class GamedataServerTests : IAsyncDisposable
{
    private const string HASH = "5f5e08b7717d531bd2c66587c9fa5ef29a962ec7";

    private readonly GamedataServer _server;
    private readonly HttpClient _http;

    public GamedataServerTests()
    {
        int port;

        using (var free = new TcpListener(IPAddress.Loopback, 0))
        {
            free.Start();
            port = ((IPEndPoint)free.LocalEndpoint).Port;
        }

        var services = new ServiceCollection()
            .AddSingleton<IGamedataFileService>(new OneBuild())
            .BuildServiceProvider();

        _server = new GamedataServer(
            services,
            Options.Create(new GamedataConfig { Enabled = true, Url = $"http://127.0.0.1:{port}" }),
            NullLoggerFactory.Instance,
            NullLogger<GamedataServer>.Instance
        );
        _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}"),
        };
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _server.StopAsync(CancellationToken.None);
        await _server.DisposeAsync();
    }

    [Theory]
    [InlineData("/gamedata/furnidata_json/" + HASH)]
    [InlineData("/gamedata/furnidata_json/0")]
    [InlineData("/gamedata/hashes")]
    public async Task ARequestWithoutAnOrigin_IsStillMarkedReadableByAnySite(string path)
    {
        await _server.StartAsync(TestContext.Current.CancellationToken);

        using var response = await _http.GetAsync(path, TestContext.Current.CancellationToken);

        ((int)response.StatusCode).Should().BeLessThan(400);
        response
            .Headers.GetValues("Access-Control-Allow-Origin")
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("*");
    }

    [Fact]
    public async Task ARequestFromTheClientsSite_GetsOneAllowOrigin()
    {
        await _server.StartAsync(TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/gamedata/furnidata_json/" + HASH
        );
        request.Headers.Add("Origin", "https://play.example.com");

        using var response = await _http.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("*");
    }

    /// <summary>Every file has the one build, by <see cref="HASH"/>.</summary>
    private sealed class OneBuild : IGamedataFileService
    {
        private static GamedataFileContent Content(string file) =>
            new(
                new GamedataFileSnapshot
                {
                    File = file,
                    Hash = HASH,
                    Size = 2,
                    BuiltAt = DateTime.UtcNow,
                },
                GamedataBytes.Compress("{}"u8.ToArray())
            );

        public Task<GamedataFileContent> GetCurrentAsync(string file, CancellationToken ct) =>
            Task.FromResult(Content(file));

        public Task<GamedataFileContent?> GetAsync(
            string file,
            string hash,
            CancellationToken ct
        ) => Task.FromResult<GamedataFileContent?>(hash == HASH ? Content(file) : null);

        public Task<GamedataFileContent> RebuildAsync(string file, CancellationToken ct) =>
            Task.FromResult(Content(file));

        public void Invalidate(string file) { }
    }
}
