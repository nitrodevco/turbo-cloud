using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Admin.Catalog;
using Turbo.Database.Entities.Assets;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Players;
using Turbo.Tests.Assets;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The catalog's pictures as the editor's Images tab and picture picker see them: the files under
/// <c>images/catalog</c> with the pages that show each, the names pages give that have no file,
/// and an uploaded GIF or PNG kept as the <c>&lt;name&gt;.gif</c> the client asks for.
/// </summary>
public sealed class AdminCatalogImagesTests : IDisposable
{
    private static readonly byte[] Gif = [.. "GIF89a"u8, 1, 0, 1, 0, 0, 0, 0, 0x3B];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];

    private readonly SqliteDb _db = new();
    private readonly AssetFolder _folder;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminCatalogImagesTests()
    {
        _folder = new AssetFolder(_db);

        var header = Page(2, null, "Furniture");
        header.ImageData = ["catalog_header_furni", " ", "teaser_sofa.gif"];
        _db.Insert(header);

        var other = Page(3, null, "Rares");
        other.ImageData = ["catalog_header_furni"];
        _db.Insert(other);
    }

    public void Dispose()
    {
        _folder.Dispose();
        _db.Dispose();
    }

    private AdminCatalogImages Images =>
        new(_db, _folder.Store, TimeProvider.System, NullLogger<AdminCatalogImages>.Instance);

    [Fact]
    public async Task Upload_KeepsTheGifTheClientAsksFor_WithThePagesThatShowIt()
    {
        var item = await Images.UploadAsync(
            null,
            "catalog_header_furni.gif",
            Gif,
            PlayerId.Parse(1),
            Ct
        );

        item.Name.Should().Be("catalog_header_furni");
        item.File.Should().Be("catalog_header_furni.gif");
        item.Size.Should().Be(Gif.Length);
        item.Pages.Select(x => x.Title).Should().Equal("Furniture", "Rares");
        File.ReadAllBytes(
                Path.Combine(_folder.Store.Root, "images/catalog/catalog_header_furni.gif")
            )
            .Should()
            .Equal(Gif);
        Images.FilePathOf("catalog_header_furni").Should().NotBeNull();
    }

    [Fact]
    public async Task Upload_KeepsAPngUnderTheGifName_AndNamesItAsGiven()
    {
        var item = await Images.UploadAsync(
            "event_banner",
            "whatever.png",
            Png,
            PlayerId.Parse(1),
            Ct
        );

        item.File.Should().Be("event_banner.gif");
        item.Pages.Should().BeEmpty();
        AdminCatalogImages
            .ContentTypeOf(File.ReadAllBytes(Images.FilePathOf("event_banner")!))
            .Should()
            .Be("image/png");
    }

    [Fact]
    public async Task Upload_RefusesWhatIsNotAPicture_AndANameWithAPath()
    {
        var notPicture = () =>
            Images.UploadAsync("x", "notes.txt", "hello"u8.ToArray(), PlayerId.Parse(1), Ct);
        var withPath = () => Images.UploadAsync("../escape", "a.gif", Gif, PlayerId.Parse(1), Ct);

        await notPicture
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*not a GIF or a PNG*");
        await withPath
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*can't be a picture's name*");
        Directory.Exists(Path.Combine(_folder.Store.Root, "images/catalog")).Should().BeFalse();
    }

    [Fact]
    public async Task List_IsThePicturesInTheFolder_HoweverTheyGotThere()
    {
        await Images.UploadAsync("unused_one", "a.gif", Gif, PlayerId.Parse(1), Ct);
        var folder = Path.Combine(_folder.Store.Root, "images/catalog");
        File.WriteAllBytes(Path.Combine(folder, "catalog_header_furni.gif"), Gif);
        File.WriteAllBytes(Path.Combine(folder, "hand_placed.png"), Png);
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "not a picture");
        // Recorded with no file (Habbo had none): not a picture the folder holds.
        _db.Insert(
            new AssetBundleEntity
            {
                Kind = AssetBundleKind.CatalogImage,
                Name = "teaser_sofa.gif",
                Source = AssetBundleSource.Habbo,
                Error = "Habbo has no such file.",
            }
        );

        var all = await Images.ListAsync(null, null, 0, 50, Ct);
        var unused = await Images.ListAsync(null, "unused", 0, 50, Ct);
        var found = await Images.ListAsync("HEADER", null, 0, 50, Ct);

        all!
            .Items.Select(x => (x.Name, x.Size, x.Pages.Length))
            .Should()
            .Equal(
                ("catalog_header_furni", Gif.Length, 2),
                ("hand_placed.png", Png.Length, 0),
                ("unused_one", Gif.Length, 0)
            );
        unused!.Items.Select(x => x.Name).Should().Equal("hand_placed.png", "unused_one");
        found!.Items.Select(x => x.Name).Should().Equal("catalog_header_furni");
        (await Images.ListAsync(null, "missing", 0, 50, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task Delete_TakesTheFileAndItsRow()
    {
        await Images.UploadAsync("old_banner", "a.gif", Gif, PlayerId.Parse(1), Ct);

        (await Images.DeleteAsync("old_banner", PlayerId.Parse(1), Ct)).Should().BeTrue();

        Images.FilePathOf("old_banner").Should().BeNull();
        (await Images.ListAsync(null, null, 0, 50, Ct))!
            .Items.Select(x => x.Name)
            .Should()
            .NotContain("old_banner");
        (await Images.DeleteAsync("old_banner", PlayerId.Parse(1), Ct)).Should().BeFalse();
    }
}
