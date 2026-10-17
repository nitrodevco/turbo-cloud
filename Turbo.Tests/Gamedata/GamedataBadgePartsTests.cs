using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Guilds;
using Turbo.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Files;
using Turbo.Gamedata.Furniture;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Settings;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// The badge imager's table, <c>badgeparts_json</c>: every part by the id badge codes are written
/// with, and the badge palette, built from the rows the editor is sent and built again when staff
/// change one.
/// </summary>
public sealed class GamedataBadgePartsTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly GamedataFileService _files;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GamedataBadgePartsTests()
    {
        _files = new GamedataFileService(
            _db,
            Options.Create(new GamedataConfig()),
            new FurnitureOfferCatalog(
                _fakes.Create<ICatalogSnapshotProvider<NormalCatalog>>(),
                _fakes.Create<ICatalogSnapshotProvider<BuildersClubCatalog>>()
            ),
            _fakes.Create<IServerSettings>(),
            new Fakes().Create<IAssetBundleStore>(),
            NullLogger<GamedataFileService>.Instance
        );

        _db.Insert(Part(1, GuildBadgePartType.Symbol, 2, "symbol_b", ""));
        _db.Insert(Part(2, GuildBadgePartType.Base, 10, "base_beams_part1", "base_beams_part2"));
        _db.Insert(Part(3, GuildBadgePartType.Symbol, 1, "symbol_a", ""));
        _db.Insert(Color(1, GuildColorSlotType.Badge, 2, "ffffff"));
        _db.Insert(Color(2, GuildColorSlotType.Badge, 1, "EEEEEE"));
        _db.Insert(Color(3, GuildColorSlotType.Primary, 1, "123456"));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task the_file_is_every_part_by_its_code_id_and_the_badge_palette_alone()
    {
        GamedataFiles.IsKnown(GamedataFiles.BADGE_PARTS).Should().BeTrue();

        var file = await ReadAsync();

        file["bases"]!
            .ToJsonString()
            .Should()
            .Be("""[{"id":10,"fileName":"base_beams_part1","maskFileName":"base_beams_part2"}]""");
        file["symbols"]!.AsArray().Select(x => (int)x!["id"]!).Should().Equal(1, 2);
        file["colors"]!
            .ToJsonString()
            .Should()
            .Be("""[{"id":1,"color":"EEEEEE"},{"id":2,"color":"ffffff"}]""");
    }

    [Fact]
    public async Task a_recoloured_palette_is_a_new_build_once_the_file_is_told()
    {
        var before = await _files.GetCurrentAsync(GamedataFiles.BADGE_PARTS, Ct);

        await using (var dbCtx = await _db.CreateDbContextAsync(Ct))
        {
            dbCtx.GuildColors.Single(x => x.Id == 1).Color = "ff8800";
            await dbCtx.SaveChangesAsync(Ct);
        }

        (await _files.GetCurrentAsync(GamedataFiles.BADGE_PARTS, Ct))
            .File.Hash.Should()
            .Be(before.File.Hash, "nothing said the rows changed");

        _files.Invalidate(GamedataFiles.BADGE_PARTS);

        var after = await _files.GetCurrentAsync(GamedataFiles.BADGE_PARTS, Ct);

        after.File.Hash.Should().NotBe(before.File.Hash);
        (await ReadAsync())["colors"]![1]!["color"]!.GetValue<string>().Should().Be("ff8800");
    }

    private async Task<JsonNode> ReadAsync()
    {
        var content = await _files.GetCurrentAsync(GamedataFiles.BADGE_PARTS, Ct);

        return JsonNode.Parse(GamedataBytes.Decompress(content.Gzipped))!;
    }

    private static GuildBadgePartEntity Part(
        int id,
        GuildBadgePartType type,
        int partId,
        string file,
        string mask
    ) =>
        new()
        {
            Id = id,
            PartType = type,
            PartId = partId,
            FileName = file,
            MaskFileName = mask,
        };

    private static GuildColorEntity Color(
        int id,
        GuildColorSlotType slot,
        int colorId,
        string color
    ) =>
        new()
        {
            Id = id,
            Slot = slot,
            ColorId = colorId,
            Color = color,
        };
}
