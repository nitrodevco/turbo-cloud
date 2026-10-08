using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Assets;
using Turbo.Admin.Configuration;
using Turbo.Gamedata;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel draws catalog icons, page images, furniture icons and badges from the external
/// variables the hotel serves the client, resolved the way the client resolves them, so staff
/// see what players see.
/// </summary>
public sealed class ClientAssetsTests
{
    private const string CLIENT_PAGE = "https://hotel.example/play/client?sso={ticket}";

    private readonly Variables _variables = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheAddresses_AreTheClients_WithItsKeysAndRelativePathsResolved()
    {
        _variables.Json = """
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
                    "https://hotel.example/play/badges/%badgename%.gif",
                    "https://images.example/c_images/"
                )
            );
    }

    [Fact]
    public async Task WithoutTheClientsPage_OnlyAddressesWithTheirOwnHostAreKept()
    {
        _variables.Json = """
            {
                "catalog.icons.url": "https://cdn.example/icon_%name%.png",
                "badge.asset.url": "/badges/%badgename%.gif"
            }
            """;

        var assets = await Assets(clientPage: "").GetAsync(Ct);

        assets.CatalogIcon.Should().Be("https://cdn.example/icon_%name%.png");
        assets.Badge.Should().BeEmpty();
    }

    [Fact]
    public async Task TheVariablesAreReadAgain_WhenTheyAreBuiltAnew()
    {
        _variables.Json = """{ "badge.asset.url": "https://a.example/%badgename%.gif" }""";
        var assets = Assets();

        (await assets.GetAsync(Ct)).Badge.Should().Be("https://a.example/%badgename%.gif");

        _variables.Json = """{ "badge.asset.url": "https://b.example/%badgename%.gif" }""";

        (await assets.GetAsync(Ct)).Badge.Should().Be("https://b.example/%badgename%.gif");
    }

    [Fact]
    public async Task VariablesThatCantBeRead_KeepTheLastAddresses()
    {
        _variables.Json = """{ "badge.asset.url": "https://a.example/%badgename%.gif" }""";
        var log = new CapturingLogger<ClientAssets>();
        var assets = Assets(log: log);

        await assets.GetAsync(Ct);
        _variables.Unreadable = true;

        (await assets.GetAsync(Ct)).Badge.Should().Be("https://a.example/%badgename%.gif");
        log.AtLeast(Microsoft.Extensions.Logging.LogLevel.Warning).Should().NotBeEmpty();
    }

    private ClientAssets Assets(
        string clientPage = CLIENT_PAGE,
        CapturingLogger<ClientAssets>? log = null
    ) =>
        new(
            _variables,
            Options.Create(new AdminConfig { ClientLoginUrl = clientPage }),
            log ?? new CapturingLogger<ClientAssets>()
        );

    /// <summary>Builds <see cref="Json"/> as the external variables, its hash its content's.</summary>
    private sealed class Variables : IGamedataFileService
    {
        public string Json { get; set; } = "{}";

        public bool Unreadable { get; set; }

        public Task<GamedataFileContent> GetCurrentAsync(string file, CancellationToken ct)
        {
            if (Unreadable)
                throw new InvalidOperationException("The database is unreachable.");

            file.Should().Be(GamedataFiles.EXTERNAL_VARIABLES);

            var content = Encoding.UTF8.GetBytes(Json);

            return Task.FromResult(
                new GamedataFileContent(
                    new GamedataFileSnapshot
                    {
                        File = file,
                        Hash = GamedataBytes.Hash(content),
                        Size = content.Length,
                        BuiltAt = DateTime.UtcNow,
                    },
                    GamedataBytes.Compress(content)
                )
            );
        }

        public Task<GamedataFileContent?> GetAsync(
            string file,
            string hash,
            CancellationToken ct
        ) => throw new NotSupportedException();

        public Task<GamedataFileContent> RebuildAsync(string file, CancellationToken ct) =>
            throw new NotSupportedException();

        public void Invalidate(string file) { }
    }
}
