using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Admin.Configuration;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Configuration;
using Turbo.Tests.Admin;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The catalog's <c>recolorable_furniture</c> layout in the editor: it is offered among the
/// layouts, and the editor is told the palette each of a page's recolorable furni is placed in -
/// the hotel's own settings, by class - leaving out furni that is not recolorable.
/// </summary>
public sealed class RecolorableCatalogTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly HttpClient _client = new();
    private readonly int _port;
    private readonly IHostedService _server;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RecolorableCatalogTests()
    {
        _port = TestPorts.Free();

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = _ => Task.FromResult(true);

        // The palettes as a hotel set them: one of its own for the wood blocks.
        var palettes = new RecolorablePalettes(
            new Monitor(
                new RoomConfig
                {
                    RecolorablePaletteToFurni = "wood=bc_wood_recolorable",
                    RecolorablePalettes = new()
                    {
                        ["default"] = "FFFFFF,00FF00",
                        ["wood"] = "672913,A73E18",
                    },
                }
            )
        );
        _fakes.Handlers["PaletteOf"] = call => palettes.PaletteOf((string)call.Args[0]!);
        _fakes.Handlers["TryGetDefinitionByName"] = call =>
            (string)call.Args[0]! == "custom_block"
                ? Definition("custom_block", RecolorableData.LOGIC_NAME)
                : null;

        (_server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            _fakes,
            NullLoggerFactory.Instance
        );
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _server.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void The_editor_offers_the_recolorable_layout()
    {
        new AdminConfig().CatalogLayouts.Should().Contain("recolorable_furniture");
    }

    [Fact]
    public async Task The_editor_is_told_each_recolorable_furnis_palette_and_nothing_of_the_rest()
    {
        await _server.StartAsync(Ct);

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"http://127.0.0.1:{_port}/api/catalog/recolorable-palettes?names=bc_block_0_recolorable*3,bc_wood_recolorable,chair_basic,custom_block"
        );
        request.Headers.Authorization = new("Bearer", "a-session");

        var response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var palettes = JsonSerializer.Deserialize<Dictionary<string, string[]>>(
            await response.Content.ReadAsStringAsync(Ct)
        )!;

        palettes
            .Keys.Should()
            .BeEquivalentTo("bc_block_0_recolorable*3", "bc_wood_recolorable", "custom_block");
        palettes["bc_block_0_recolorable*3"].Should().Equal("FFFFFF", "00FF00");
        palettes["bc_wood_recolorable"].Should().Equal("672913", "A73E18");
        palettes["custom_block"].Should().Equal("FFFFFF", "00FF00");
    }

    private static FurnitureDefinitionSnapshot Definition(string name, string logic) =>
        new()
        {
            Id = 1000,
            SpriteId = 1000,
            Name = name,
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            LogicName = logic,
            TotalStates = 2,
            Width = 1,
            Length = 1,
            StackHeight = Altitude.FromInt(100),
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
            UsagePolicy = FurnitureUsageType.Everybody,
            ExtraData = null,
        };

    private sealed class Monitor(RoomConfig value) : IOptionsMonitor<RoomConfig>
    {
        public RoomConfig CurrentValue => value;

        public RoomConfig Get(string? name) => value;

        public IDisposable? OnChange(Action<RoomConfig, string?> listener) => null;
    }
}
