using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Assets;
using Turbo.Admin.Configuration;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel draws catalog icons, page images, furniture icons and badges from the client's own
/// config, resolved the way the client resolves it, so staff see what players see.
/// </summary>
public sealed class ClientAssetsTests
{
    private const string CONFIG_URL = "https://hotel.example/config/nitro-config.json";

    private readonly ConfigHost _host = new();
    private readonly ManualTimeProvider _clock = new(
        new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc)
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheAddresses_AreTheClients_WithItsKeysAndRelativePathsResolved()
    {
        _host.Json = """
            {
                "image.library.url": "//images.example/c_images/",
                "catalog.icons.url": "https://cdn.example/catalog-icons/icon_%name%.png",
                "asset.urls.catalog": "${image.library.url}catalogue/%name%.gif",
                "asset.urls.icons.furni": "/bundled/furniture/icons/%libname%%param%_icon.png",
                "badge.asset.url": "badges/%badgename%.gif",
                "socket.url": "ws://localhost:9001",
                "log.debug": false
            }
            """;

        var assets = await Assets().GetAsync(Ct);

        assets
            .Should()
            .Be(
                new ClientAssetsResponse(
                    "https://cdn.example/catalog-icons/icon_%name%.png",
                    "https://images.example/c_images/catalogue/%name%.gif",
                    "https://hotel.example/bundled/furniture/icons/%libname%%param%_icon.png",
                    "https://hotel.example/config/badges/%badgename%.gif"
                )
            );
    }

    [Fact]
    public async Task WithNoClientConfig_ThereAreNoAddresses_AndNothingIsFetched()
    {
        (await Assets(url: "").GetAsync(Ct)).Should().Be(ClientAssetsResponse.NONE);

        _host.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task TheConfigIsReadOnce_ThenAgainWhenItsTimeIsUp()
    {
        _host.Json = """{ "badge.asset.url": "https://a.example/%badgename%.gif" }""";
        var assets = Assets();

        await assets.GetAsync(Ct);
        _host.Json = """{ "badge.asset.url": "https://b.example/%badgename%.gif" }""";

        (await assets.GetAsync(Ct)).Badge.Should().Be("https://a.example/%badgename%.gif");

        _clock.Advance(TimeSpan.FromMinutes(10));

        (await assets.GetAsync(Ct)).Badge.Should().Be("https://b.example/%badgename%.gif");
        _host.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task AConfigThatCantBeRead_KeepsTheLastAddresses()
    {
        _host.Json = """{ "badge.asset.url": "https://a.example/%badgename%.gif" }""";
        var assets = Assets();

        await assets.GetAsync(Ct);
        _host.Unreachable = true;
        _clock.Advance(TimeSpan.FromMinutes(10));

        (await assets.GetAsync(Ct)).Badge.Should().Be("https://a.example/%badgename%.gif");
    }

    private ClientAssets Assets(string url = CONFIG_URL) =>
        new(
            Options.Create(new AdminConfig { ClientConfigUrl = url }),
            _clock,
            new CapturingLogger<ClientAssets>(),
            _host
        );

    /// <summary>Serves <see cref="Json"/> as the client config, and records each request.</summary>
    private sealed class ConfigHost : HttpMessageHandler
    {
        private readonly ConcurrentQueue<string> _requests = new();

        public string Json { get; set; } = "{}";

        public bool Unreachable { get; set; }

        public IReadOnlyList<string> Requests => [.. _requests];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            _requests.Enqueue(request.RequestUri!.ToString());

            if (Unreachable)
                throw new HttpRequestException("No route to host.");

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Json, Encoding.UTF8, "application/json"),
                }
            );
        }
    }
}
