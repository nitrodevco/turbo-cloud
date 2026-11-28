using System.Buffers.Binary;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Media;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Entities.Hotel;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Players;
using Turbo.Tests.Assets;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Media;

/// <summary>
/// The media library as staff search it: one index over the asset folder's image library and
/// pictures, each image with the places that show it, uploads kept where a publish sends them,
/// and an image anything shows kept from deletion (the r60 brief's proof cases).
/// </summary>
public sealed class MediaLibraryTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly AssetFolder _folder;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public MediaLibraryTests()
    {
        _folder = new AssetFolder(_db);
    }

    public void Dispose()
    {
        _folder.Dispose();
        _db.Dispose();
    }

    private MediaLibrary Library =>
        new(_db, _folder.Store, TimeProvider.System, NullLogger<MediaLibrary>.Instance);

    private static byte[] Png(int width, int height)
    {
        var data = new byte[33];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        signature.CopyTo(data, 0);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(8), 13);
        "IHDR"u8.CopyTo(data.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(20), height);

        return data;
    }

    private void Library_(string path, byte[] data)
    {
        var full = Path.Combine(_folder.Store.Root, "c_images", path);

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, data);
    }

    private void ShowOnArticleAndNotice()
    {
        _db.Insert(
            new PromoArticleEntity
            {
                Title = "Summer is here",
                BodyText = "",
                ButtonText = "",
                LinkContent = "",
                ImageUrl = "web_promo/summer.png",
            }
        );
        _db.Insert(
            new GamedataVariableEntity
            {
                Key = "notifications.new_feature.image.summer",
                Value = "\"https://images.example.com/c_images/web_promo/summer.png\"",
            }
        );
    }

    [Fact]
    public async Task Search_FindsALibraryPictureInItsFolder_WithTheNoticeAndArticleShowingIt()
    {
        Library_("web_promo/summer.png", Png(160, 120));
        Library_("web_promo/winter.png", Png(1, 1));
        ShowOnArticleAndNotice();

        var found = await Library.SearchAsync(new MediaQuery("summer"), Ct);
        var image = await Library.GetAsync(found.Items.Single().Id, Ct);

        found.Total.Should().Be(1);
        image!.Id.Should().Be("library:web_promo/summer.png");
        image.Kind.Should().Be("promo");
        image.Source.Should().Be(MediaLibrary.SOURCE_LIBRARY);
        image.Values.Relative.Should().Be("web_promo/summer.png");
        (image.Width, image.Height).Should().Be((160, 120));
        image
            .UsedOn!.Select(x => (x.Area, x.Title))
            .Should()
            .BeEquivalentTo([("notice", "summer"), ("hotel-view", "Summer is here")]);
        (await Library.SearchAsync(new MediaQuery(Used: "unused"), Ct))
            .Items.Select(x => x.Path)
            .Should()
            .Equal("web_promo/winter.png");
    }

    [Fact]
    public async Task Search_LeavesBadgesToTheLibrarysOwnTab()
    {
        // Badges are listed on Gamedata > Library > Badges, a page at a time from their records.
        var badge = _folder.Store.FullPathOf(AssetBundleKind.Badge, "ACH_Login1.gif");

        Directory.CreateDirectory(Path.GetDirectoryName(badge)!);
        await File.WriteAllBytesAsync(badge, [.. "GIF89a"u8, 1, 0, 1, 0, 0, 0, 0, 0x3B], Ct);
        Library_("web_promo/summer.png", Png(1, 1));

        var found = await Library.SearchAsync(new MediaQuery(), Ct);

        found.Items.Select(x => x.Id).Should().Equal("library:web_promo/summer.png");
        found.Facets.Kinds.Keys.Should().Equal("promo");
        (await Library.GetAsync("badge:ACH_Login1.gif", Ct)).Should().BeNull();
    }

    [Fact]
    public async Task Search_LeavesFurnitureIconsOut()
    {
        // A publish draws them from each furniture's bundle; no image field names one.
        var icon = _folder.Store.FullPathOf(AssetBundleKind.FurniIcon, "throne_icon.png");

        Directory.CreateDirectory(Path.GetDirectoryName(icon)!);
        await File.WriteAllBytesAsync(icon, Png(1, 1), Ct);
        Library_("web_promo/summer.png", Png(1, 1));

        var found = await Library.SearchAsync(new MediaQuery(), Ct);

        found.Items.Select(x => x.Id).Should().Equal("library:web_promo/summer.png");
        found.Facets.Kinds.Keys.Should().Equal("promo");
        (await Library.GetAsync("furni-icon:throne_icon.png", Ct)).Should().BeNull();
    }

    [Fact]
    public async Task Search_WithoutTheCatalog_LeavesItsPicturesAndIconsToTheCatalogsOwnPage()
    {
        // The picker still finds them for a catalog page's fields; the media library page asks without.
        foreach (
            var (kind, name) in new[]
            {
                (AssetBundleKind.CatalogImage, "catalog_header.gif"),
                (AssetBundleKind.CatalogIcon, "icon_12.png"),
            }
        )
        {
            var path = _folder.Store.FullPathOf(kind, name);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, Png(1, 1), Ct);
        }

        Library_("web_promo/summer.png", Png(1, 1));

        var all = await Library.SearchAsync(new MediaQuery(), Ct);
        var without = await Library.SearchAsync(new MediaQuery(Catalog: false), Ct);

        all.Items.Select(x => x.Id)
            .Should()
            .BeEquivalentTo(
                "catalog:catalog_header.gif",
                "catalog-icon:icon_12.png",
                "library:web_promo/summer.png"
            );
        without.Items.Select(x => x.Id).Should().Equal("library:web_promo/summer.png");
        without.Total.Should().Be(1);
        without.Facets.Kinds.Keys.Should().Equal("promo");
    }

    [Fact]
    public async Task CatalogIcons_AreKeptByNumber_AndChangedOnlyThroughTheCatalogsOwnMethods()
    {
        var player = PlayerId.Parse(1);
        var first = await Library.UploadCatalogIconAsync(900, Png(1, 1), player, Ct);
        var second = await Library.UploadCatalogIconAsync(20, Png(1, 1), player, Ct);
        var notPng = await Library.UploadCatalogIconAsync(
            7,
            [.. "GIF89a"u8, 1, 0, 1, 0, 0, 0, 0, 0x3B],
            player,
            Ct
        );

        (first.Icon!.Id, first.Icon.Values.CatalogIcon)
            .Should()
            .Be(("catalog-icon:icon_900.png", 900));
        second.Icon!.Id.Should().Be(MediaLibrary.CatalogIconId(20));
        notPng.Icon.Should().BeNull();
        notPng.Error.Should().Contain("name it .gif");
        // Listed by number, not letter by letter.
        (await Library.SearchAsync(new MediaQuery(Kind: MediaLibrary.KIND_CATALOG_ICON), Ct))
            .Items.Select(x => x.Name)
            .Should()
            .Equal("icon_20.png", "icon_900.png");

        // The media library's own delete leaves the catalog's files to the catalog.
        var refused = await Library.DeleteAsync(MediaLibrary.CatalogIconId(900), player, Ct);

        refused.Error.Should().Contain("Catalog > Images");
        File.Exists(_folder.Store.FullPathOf(AssetBundleKind.CatalogIcon, "icon_900.png"))
            .Should()
            .BeTrue();

        (await Library.DeleteCatalogIconAsync(900, player, Ct)).Error.Should().BeNull();
        File.Exists(_folder.Store.FullPathOf(AssetBundleKind.CatalogIcon, "icon_900.png"))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task Upload_KeepsEachImageRecordedForAPublish_AndRefusesTheRestWithWhy()
    {
        var result = await Library.UploadAsync(
            [
                ("a.png", Png(2, 2)),
                ("b.png", Png(3, 3)),
                ("c.png", Png(4, 4)),
                ("notes.png", "hello"u8.ToArray()),
                ("gif.png", [.. "GIF89a"u8, 1, 0, 1, 0, 0, 0, 0, 0x3B]),
            ],
            "web_promo",
            PlayerId.Parse(1),
            Ct
        );
        var escape = await Library.UploadAsync(
            [("x.png", Png(1, 1))],
            "../../outside",
            PlayerId.Parse(1),
            Ct
        );

        result
            .Items.Select(x => x.Id)
            .Should()
            .Equal("library:web_promo/a.png", "library:web_promo/b.png", "library:web_promo/c.png");
        result.Refused.Select(x => x.File).Should().Equal("notes.png", "gif.png");
        result.Refused[1].Why.Should().Contain("name it .gif");
        escape.Items.Should().BeEmpty();
        escape.Refused.Should().ContainSingle().Which.Why.Should().Contain("can't be a path");
        (await Library.SearchAsync(new MediaQuery("web_promo", Source: "upload"), Ct))
            .Total.Should()
            .Be(3);
        (await _folder.Store.ListFilesAsync(Ct))
            .Select(x => x.Path)
            .Should()
            .Equal(
                "c_images/web_promo/a.png",
                "c_images/web_promo/b.png",
                "c_images/web_promo/c.png"
            );
    }

    [Fact]
    public async Task Delete_IsRefusedWhileAnythingShowsTheImage_WithWhatShowsIt()
    {
        Library_("web_promo/summer.png", Png(1, 1));
        ShowOnArticleAndNotice();
        Library_("web_promo/unused.png", Png(1, 1));

        var refused = await Library.DeleteAsync(
            "library:web_promo/summer.png",
            PlayerId.Parse(1),
            Ct
        );
        var done = await Library.DeleteAsync("library:web_promo/unused.png", PlayerId.Parse(1), Ct);

        refused.Error.Should().NotBeNull();
        refused.UsedOn!.Select(x => x.Area).Should().BeEquivalentTo(["notice", "hotel-view"]);
        File.Exists(Path.Combine(_folder.Store.Root, "c_images/web_promo/summer.png"))
            .Should()
            .BeTrue();
        done.Error.Should().BeNull();
        File.Exists(Path.Combine(_folder.Store.Root, "c_images/web_promo/unused.png"))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task Edit_TagsAnImageItIsFoundBy_AndRenamesItInItsFolder()
    {
        Library_("web_promo/x1.png", Png(1, 1));

        var edited = await Library.EditAsync(
            "library:web_promo/x1.png",
            new MediaEditRequest("beach.png", ["Holiday", "sun"]),
            PlayerId.Parse(1),
            Ct
        );

        edited.Image!.Id.Should().Be("library:web_promo/beach.png");
        (await Library.SearchAsync(new MediaQuery("holiday"), Ct))
            .Items.Select(x => (x.Path, string.Join(',', x.Tags)))
            .Should()
            .Equal(("web_promo/beach.png", "holiday,sun"));
    }
}
