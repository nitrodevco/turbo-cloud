using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Assets.Conversion;
using Turbo.Assets.Nitro;
using Turbo.Database.Entities.Assets;
using Turbo.Gamedata.HandItems;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Assets;

public sealed class HandItemCatalogTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly AssetFolder _folder;
    private readonly Fakes _fakes = new();
    private readonly Dictionary<string, string> _labels = new() { ["handitem1128"] = "Coffee" };
    private int _nextId;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public HandItemCatalogTests()
    {
        _folder = new AssetFolder(_db);
        _fakes.Handlers[nameof(IHotelTextProvider.GetTextsByPrefixAsync)] = _ =>
            Task.FromResult(new HotelTexts(_labels));
    }

    private HandItemCatalog Catalog() =>
        new(
            _db,
            _folder.Store,
            _fakes.Create<IHotelTextProvider>(),
            NullLogger<HandItemCatalog>.Instance
        );

    [Fact]
    public async Task Maps_server_ids_to_parts_and_ignores_invalid_parameters()
    {
        await Actions(
            """[{"id":"1128","value":"311"},{"id":"1128","value":"999"},{"id":"1200","value":"400"},{"id":"2000000000","value":"500"},{"id":"default","value":"1"},{"id":"999999999","value":"-1"},{"id":"-2","value":"311"},{"id":"2","value":"0"}]"""
        );
        await Figure();
        var catalog = Catalog();
        var result = await catalog.GetAsync(Ct);
        result.Message.Should().BeNull();
        result.Items.Select(x => x.Id).Should().Equal(1128, 1200, 2000000000);
        var item = result.Items[0];
        item.AssetPartId.Should().Be(311);
        item.Name.Should().Be("Coffee");
        item.Library.Should().Be("hh_human_item");
        item.AssetAvailable.Should().BeTrue();
        item.PreviewAvailable.Should().BeTrue();
        result.Items[1].Name.Should().BeNull();
        result.Items[1].AssetAvailable.Should().BeFalse();
        var png = await catalog.GetPreviewAsync(1128, Ct);
        png.Should().NotBeNull();
        var image = RgbaImage.Decode(png!);
        image.Width.Should().Be(2);
        image.Pixels.Should().Equal(255, 0, 0, 255, 0, 0, 255, 255);
        (await catalog.GetPreviewAsync(311, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task Updates_files_labels_deletions_and_restart_without_a_seed_catalogue()
    {
        await Actions("""[{"id":"1128","value":"311"}]""");
        await Figure();
        var catalog = Catalog();
        var first = await catalog.GetAsync(Ct);
        _labels["handitem1128"] = "Tea";
        (await catalog.GetAsync(Ct)).Items[0].Name.Should().Be("Tea");
        await Actions("""[{"id":"1128","value":"312"},{"id":"1300","value":"311"}]""");
        var updated = await catalog.GetAsync(Ct);
        updated.Revision.Should().NotBe(first.Revision);
        updated.Items[0].AssetPartId.Should().Be(312);
        (await Catalog().GetAsync(Ct)).Items.Should().Equal(updated.Items);
        _folder.Store.Delete(AssetBundleKind.Figure, "hh_human_item");
        (await catalog.GetAsync(Ct))
            .Items.Should()
            .OnlyContain(x => !x.AssetAvailable && !x.PreviewAvailable);
        _folder.Store.Delete(AssetBundleKind.Template, "habbo-avatar-render-lib");
        var missing = await catalog.GetAsync(Ct);
        missing.Items.Should().BeEmpty();
        missing.Message.Should().Contain("actions");
    }

    [Fact]
    public async Task Malformed_local_template_preserves_last_good_snapshot_and_recovers()
    {
        var catalog = Catalog();
        (await catalog.GetAsync(Ct)).Message.Should().NotBeNull();
        await Actions("""[{"id":"1128","value":"311"}]""");
        var good = await catalog.GetAsync(Ct);
        await Save(
            AssetBundleKind.Template,
            "habbo-avatar-render-lib",
            new Dictionary<string, byte[]> { ["actions.json"] = Encoding.UTF8.GetBytes("{") }
        );
        var stale = await catalog.GetAsync(Ct);
        stale.Items.Should().Equal(good.Items);
        stale.Message.Should().Contain("last readable");
        await Actions("""[{"id":"1300","value":"311"}]""");
        var recovered = await catalog.GetAsync(Ct);
        recovered.Items.Should().ContainSingle(x => x.Id == 1300);
        recovered.Message.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preview_resolves_source_alias_and_flips_the_standalone_frame(
        bool manifestAlias
    )
    {
        await Actions("""[{"id":"1128","value":"311"}]""");
        await Figure(alias: true, manifestAlias: manifestAlias);
        var png = await Catalog().GetPreviewAsync(1128, Ct);
        RgbaImage.Decode(png!).Pixels.Should().Equal(0, 0, 255, 255, 255, 0, 0, 255);
    }

    [Fact]
    public async Task Unreadable_and_missing_art_do_not_claim_a_preview()
    {
        await Actions("""[{"id":"1128","value":"311"}]""");
        await Figure();
        await File.WriteAllBytesAsync(
            _folder.Store.FullPathOf(AssetBundleKind.Figure, "hh_human_item"),
            [1, 2, 3],
            Ct
        );
        var result = await Catalog().GetAsync(Ct);
        result.Items.Should().ContainSingle(x => !x.AssetAvailable && !x.PreviewAvailable);
        (await Catalog().GetPreviewAsync(1128, Ct)).Should().BeNull();
    }

    [Theory]
    [InlineData("{\"assets\":{}}", "{\"frames\":{}}")]
    [InlineData("{\"assets\":[],\"aliases\":{}}", "{\"frames\":{}}")]
    [InlineData("{\"assets\":[]}", "{\"frames\":[]}")]
    [InlineData("{\"assets\":[]}", "{\"frames\":{\"broken\":{\"frame\":[]}}}")]
    public async Task Invalid_art_metadata_preserves_last_readable_preview_and_recovers(
        string manifest,
        string frames
    )
    {
        await Actions("""[{"id":"1128","value":"311"}]""");
        await Figure();
        var catalog = Catalog();
        var good = await catalog.GetAsync(Ct);
        var png = await catalog.GetPreviewAsync(1128, Ct);
        await Save(
            AssetBundleKind.Figure,
            "hh_human_item",
            new Dictionary<string, byte[]>
            {
                ["hh_human_item.json"] = Encoding.UTF8.GetBytes(manifest),
                ["hh_human_item_spritesheet.json"] = Encoding.UTF8.GetBytes(frames),
            },
            """[{"id":311,"type":"ri"}]"""
        );
        var stale = await catalog.GetAsync(Ct);
        stale.Items.Should().Equal(good.Items);
        stale.Message.Should().Contain("last readable");
        (await catalog.GetPreviewAsync(1128, Ct)).Should().Equal(png!);
        await Figure();
        (await catalog.GetAsync(Ct)).Message.Should().BeNull();
    }

    private Task Actions(string parameters) =>
        Save(
            AssetBundleKind.Template,
            "habbo-avatar-render-lib",
            new Dictionary<string, byte[]>
            {
                ["actions.json"] = Encoding.UTF8.GetBytes(
                    "{\"actions\":[{\"id\":\"CarryItem\",\"state\":\"cri\",\"assetPartDefinition\":\"crr\",\"params\":"
                        + parameters
                        + "}]}"
                ),
            }
        );

    private Task Figure(bool alias = false, bool manifestAlias = false)
    {
        var asset = alias
            ? """{"name":"h_crr_311_2_0","source":"original","flipH":true},{"name":"original"}"""
            : """{"name":"h_crr_311_2_0"}""";
        var frameName = alias ? "original" : "h_crr_311_2_0";
        var data = manifestAlias
            ? """{"assets":[{"name":"original"}],"aliases":[{"name":"h_crr_311_2_0","link":"original","flipH":true}]}"""
            : "{\"assets\":[" + asset + "]}";
        return Save(
            AssetBundleKind.Figure,
            "hh_human_item",
            new Dictionary<string, byte[]>
            {
                ["hh_human_item.json"] = Encoding.UTF8.GetBytes(data),
                ["hh_human_item.png"] = new RgbaImage(
                    3,
                    1,
                    [0, 255, 0, 255, 255, 0, 0, 255, 0, 0, 255, 255]
                ).EncodePng(),
                ["hh_human_item_spritesheet.json"] = JsonSerializer.SerializeToUtf8Bytes(
                    new
                    {
                        frames = new Dictionary<string, object>
                        {
                            [frameName] = new
                            {
                                frame = new
                                {
                                    x = 1,
                                    y = 0,
                                    w = 2,
                                    h = 1,
                                },
                            },
                        },
                    }
                ),
            },
            """[{"id":311,"type":"ri"}]"""
        );
    }

    private async Task Save(
        AssetBundleKind kind,
        string name,
        Dictionary<string, byte[]> files,
        string? mapEntries = null
    )
    {
        var hash = await _folder.Store.WriteAsync(
            kind,
            name,
            NitroBundle.Create(files).Write(),
            Ct
        );
        await using var db = await _db.CreateDbContextAsync(Ct);
        var row =
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(
                db.AssetBundles,
                x => x.Kind == kind && x.Name == name,
                Ct
            );
        if (row is null)
        {
            row = new AssetBundleEntity
            {
                Id = ++_nextId,
                Kind = kind,
                Name = name,
                Source = AssetBundleSource.Upload,
            };
            db.AssetBundles.Add(row);
        }
        row.Hash = hash;
        row.MapEntries = mapEntries;
        await db.SaveChangesAsync(Ct);
    }

    public void Dispose()
    {
        _folder.Dispose();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
