using System.Text;
using System.Text.Json;
using FluentAssertions;
using Turbo.Assets.Nitro;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// Comparing what a publish target holds with the hotel's copy, through the real store and publish
/// service and a folder target. A live hotel 2026-10-26: a target had 20 template bundles pending as
/// changed, and only reading both copies by hand showed 17 differed in the client build stamp alone
/// and 3 in what players get (a window gone from the inventory, 11 marketplace texts gone in 13
/// languages, 18 new images in the window manager).
/// </summary>
public sealed class AssetPublishCompareTests : IDisposable
{
    private const string OLD_STAMP = "98_classic-js-5f1c0a2b9d3e";
    private const string NEW_STAMP = "99_classic-js-69df354f4175";

    private readonly AssetPublishHotel _hotel = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _hotel.Dispose();

    [Fact]
    public async Task A_bundle_whose_only_difference_is_the_build_stamp_is_stamp_only()
    {
        var target = await PublishedAsync(
            "habbo-avatar-editor-com",
            Templates(OLD_STAMP, ("AvatarEditor", Window(("avatarEditor", null))))
        );
        await PutTemplateAsync(
            "habbo-avatar-editor-com",
            Templates(NEW_STAMP, ("AvatarEditor", Window(("avatarEditor", null))))
        );

        var compared = (await _hotel.Publishing.CompareAsync(target, null, Ct))!;

        compared.Total.Should().Be(1);
        var bundle = compared.Items.Should().ContainSingle().Subject;
        bundle.Result.Should().Be(AssetCompareResult.StampOnly);
        (bundle.StampBefore, bundle.StampAfter).Should().Be((OLD_STAMP, NEW_STAMP));
        (bundle.Windows.Length, bundle.Texts.Length, bundle.Entries.Length).Should().Be((0, 0, 0));
        (bundle.Source, bundle.Revision, bundle.TargetAsRecorded)
            .Should()
            .Be((AssetBundleSource.Habbo, "client:" + NEW_STAMP, true));
        bundle.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_real_change_names_the_windows_texts_and_images_that_changed()
    {
        var target = await PublishedAsync(
            "habbo-catalog-com",
            Templates(
                OLD_STAMP,
                ("make_nft_marketplace_offer_xml", Window(("offer", "${nft.offer}"))),
                (
                    "marketplace_search_advanced_xml",
                    Window(
                        ("search", "${search.title}"),
                        ("offer_source_1", "${offer.source.1}"),
                        ("sort", null)
                    )
                )
            ),
            ("DefaultLocalizations_en.json", Texts(("emerald.title", "Emeralds"), ("ok", "OK"))),
            ("DefaultLocalizations_de.json", Texts(("emerald.title", "Smaragde"), ("ok", "OK"))),
            ("habbo-catalog-com-icon.png", Png(16, 16))
        );
        await PutTemplateAsync(
            "habbo-catalog-com",
            Templates(
                NEW_STAMP,
                ("cursed_dragon_adoption_xml", Window(("adopt", "${dragon.adopt}"))),
                (
                    "marketplace_search_advanced_xml",
                    Window(("search", "${search.title}"), ("sort", "${sort.by}"))
                )
            ),
            ("DefaultLocalizations_en.json", Texts(("ok", "Okay"))),
            ("DefaultLocalizations_de.json", Texts(("ok", "OK"))),
            ("habbo-catalog-com-icon.png", Png(32, 24)),
            ("habbo-catalog-com-dragon.png", Png(64, 64))
        );

        var bundle = (
            await _hotel.Publishing.CompareAsync(
                target,
                "bundled/templates/habbo-catalog-com.nitro",
                Ct
            )
        )!.Items.Single();

        bundle.Result.Should().Be(AssetCompareResult.Changed);
        bundle
            .Windows.Select(x => (x.Id, x.Change))
            .Should()
            .Equal(
                ("habbo-catalog-com/cursed_dragon_adoption_xml", AssetDiffChange.Added),
                ("habbo-catalog-com/make_nft_marketplace_offer_xml", AssetDiffChange.Removed),
                ("habbo-catalog-com/marketplace_search_advanced_xml", AssetDiffChange.Changed)
            );
        var search = bundle.Windows[2];
        search.ElementsRemoved.Should().Equal("offer_source_1");
        search.ElementsChanged.Should().Equal("sort");
        search.ElementsAdded.Should().BeEmpty();
        search.TextKeysAdded.Should().Equal("sort.by");
        search.TextKeysRemoved.Should().Equal("offer.source.1");
        bundle
            .Texts.Select(x => (x.Language, x.Added, x.Removed, x.Changed))
            .Should()
            .Equal(("de", 0, 1, 0), ("en", 0, 1, 1));
        bundle
            .Texts[1]
            .Edits.Select(x => (x.Key, x.Change, x.Before, x.After))
            .Should()
            .Equal(
                ("emerald.title", AssetDiffChange.Removed, "Emeralds", null),
                ("ok", AssetDiffChange.Changed, "OK", "Okay")
            );
        bundle
            .Entries.Select(x => (x.Name, x.Change, x.PixelsBefore, x.PixelsAfter))
            .Should()
            .Equal(
                (
                    "habbo-catalog-com-dragon.png",
                    AssetDiffChange.Added,
                    null,
                    new AssetPixelSize(64, 64)
                ),
                (
                    "habbo-catalog-com-icon.png",
                    AssetDiffChange.Changed,
                    new AssetPixelSize(16, 16),
                    new AssetPixelSize(32, 24)
                )
            );
        (bundle.StampBefore, bundle.StampAfter).Should().Be((OLD_STAMP, NEW_STAMP));

        var before = await _hotel.Publishing.ReadComparedImageAsync(
            target,
            "bundled/templates/habbo-catalog-com.nitro",
            "habbo-catalog-com-icon.png",
            fromTarget: true,
            Ct
        );
        var after = await _hotel.Publishing.ReadComparedImageAsync(
            target,
            "bundled/templates/habbo-catalog-com.nitro",
            "habbo-catalog-com-icon.png",
            fromTarget: false,
            Ct
        );
        before.Should().Equal(Png(16, 16));
        after.Should().Equal(Png(32, 24));
    }

    [Fact]
    public async Task Compare_all_takes_the_changed_files_and_reads_each_copy_from_the_target_once()
    {
        var target = await PublishedAsync(
            "habbo-avatar-editor-com",
            Templates(OLD_STAMP, ("AvatarEditor", Window(("avatarEditor", null))))
        );
        await PutTemplateAsync(
            "habbo-avatar-editor-com",
            Templates(NEW_STAMP, ("AvatarEditor", Window(("avatarEditor", null))))
        );
        await _hotel.PutBundleAsync("sofa", [1, 2, 3], Ct);

        (await _hotel.Publishing.CompareAsync(target, null, Ct))!
            .Items.Select(x => (x.File.Name, x.Result))
            .Should()
            .Equal(("habbo-avatar-editor-com", AssetCompareResult.StampOnly));

        // Read once: gone from the target now, the copy read is still the one compared.
        File.Delete(
            Path.Combine(
                _hotel.TargetFolder,
                "bundled",
                "templates",
                "habbo-avatar-editor-com.nitro"
            )
        );

        (await _hotel.Publishing.CompareAsync(target, null, Ct))!
            .Items.Single()
            .Result.Should()
            .Be(AssetCompareResult.StampOnly);
    }

    [Fact]
    public async Task A_copy_that_cannot_be_read_says_why()
    {
        var target = await PublishedAsync(
            "habbo-avatar-editor-com",
            Templates(OLD_STAMP, ("AvatarEditor", Window(("avatarEditor", null))))
        );
        await PutTemplateAsync(
            "habbo-avatar-editor-com",
            Templates(NEW_STAMP, ("AvatarEditor", Window(("avatarEditor", null))))
        );
        File.Delete(
            Path.Combine(
                _hotel.TargetFolder,
                "bundled",
                "templates",
                "habbo-avatar-editor-com.nitro"
            )
        );

        (await _hotel.Publishing.CompareAsync(target, null, Ct))!
            .Items.Single()
            .Result.Should()
            .Be(AssetCompareResult.NotOnTarget);

        Directory.Delete(_hotel.TargetFolder, recursive: true);

        var unreachable = (await _hotel.Publishing.CompareAsync(target, null, Ct))!.Items.Single();

        unreachable.Result.Should().Be(AssetCompareResult.Unreadable);
        unreachable.Problem.Should().StartWith("The target could not be reached: The folder ");
        (await _hotel.Publishing.CompareAsync(target + 1, null, Ct)).Should().BeNull();
        (await _hotel.Publishing.CompareAsync(target, "bundled/templates/none.nitro", Ct))
            .Should()
            .BeNull();
    }

    /// <summary>A folder target the template bundle was published to; its id.</summary>
    private async Task<int> PublishedAsync(string name, params (string, byte[])[] files)
    {
        await PutTemplateAsync(name, files);

        var target = await _hotel.AddFolderTargetAsync(Ct);

        await _hotel.PublishAsync(target.Id, Ct);

        return target.Id;
    }

    private Task PutTemplateAsync(string name, params (string File, byte[] Data)[] files)
    {
        var stamp = files
            .Where(x => x.File == "templates.json")
            .Select(x => JsonDocument.Parse(x.Data).RootElement.GetProperty("version").GetString())
            .FirstOrDefault();
        var bundle = NitroBundle
            .Create(
                new Dictionary<string, byte[]>(
                    files.Select(x =>
                        KeyValuePair.Create(
                            x.File.Replace("{name}", name, StringComparison.Ordinal),
                            x.File.EndsWith(".json", StringComparison.Ordinal)
                                ? Prefixed(x.Data, name)
                                : x.Data
                        )
                    )
                )
            )
            .Write();

        return _hotel.PutBundleAsync(
            name,
            bundle,
            Ct,
            revision: $"client:{stamp}",
            kind: AssetBundleKind.Template
        );
    }

    /// <summary>A templates.json's templates are keyed by their library: put it in.</summary>
    private static byte[] Prefixed(byte[] data, string library) =>
        Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(data).Replace("{library}", library, StringComparison.Ordinal)
        );

    /// <summary>A templates.json as the client release packs it: its stamp and its templates by id.</summary>
    private static (string, byte[]) Templates(
        string stamp,
        params (string Id, object Window)[] windows
    ) =>
        (
            "templates.json",
            JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    library = "{library}",
                    version = stamp,
                    templates = windows.ToDictionary(x => "{library}/" + x.Id, x => x.Window),
                }
            )
        );

    /// <summary>A window of named text elements, each with its caption (or none), under one container.</summary>
    private static object Window(params (string Name, string? Caption)[] elements) =>
        new
        {
            name = "window",
            width = 100,
            height = 100,
            elements = new object[]
            {
                new
                {
                    tag = "container",
                    children = elements
                        .Select(x =>
                            x.Caption is null
                                ? (object)new { tag = "text", name = x.Name }
                                : new
                                {
                                    tag = "text",
                                    name = x.Name,
                                    caption = x.Caption,
                                }
                        )
                        .ToArray(),
                },
            },
        };

    private static byte[] Texts(params (string Key, string Text)[] texts) =>
        JsonSerializer.SerializeToUtf8Bytes(texts.ToDictionary(x => x.Key, x => x.Text));

    /// <summary>The start of a PNG: its signature and the header saying its size.</summary>
    private static byte[] Png(int width, int height)
    {
        var png = new byte[33];

        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 }.CopyTo(png, 0);
        "IHDR"u8.CopyTo(png.AsSpan(12));
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20), height);

        return png;
    }
}
