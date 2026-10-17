using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Assets.Conversion;
using Turbo.Assets.Nitro;
using Turbo.Database.Entities.Assets;
using Turbo.Gamedata.Assets;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.Habbo.Client;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// A sync from Habbo: what Habbo lists is downloaded and converted into the bundle folder with a
/// row each; the next sync skips what is up to date and what failed in a way that won't pass, and
/// tries again what may; an upload is never replaced. A Habbo check starts one only when that is
/// on and no job runs.
/// </summary>
public sealed class AssetSyncTests : IDisposable
{
    private const string VARIABLES = "https://www.habbo.com/gamedata/external_variables/0";
    private const string FURNIDATA = "https://www.habbo.com/gamedata/furnidata_json/0";
    private const string TEXTS = "https://www.habbo.com/gamedata/external_flash_texts/0";
    private const string FURNI = "https://images.habbo.com/dcr/hof_furni";
    private const string GORDON = "https://images.habbo.com/gordon/flash-assets-PRODUCTION-1";

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HabboHost _habbo = new();
    private readonly AssetFolder _folder;
    private readonly AssetJobs _jobs;
    private readonly AssetSyncService _sync;

    public AssetSyncTests()
        : this(new AssetBundleConfig { DownloadConcurrency = 3, ConvertConcurrency = 2 }) { }

    private AssetSyncTests(AssetBundleConfig config)
    {
        _folder = new AssetFolder(_db, config);
        _fakes.Handlers["CreateClient"] = _ => new HttpClient(_habbo, disposeHandler: false);
        _jobs = new AssetJobs(
            Options.Create(config),
            _fakes.Create<IHostApplicationLifetime>(),
            TimeProvider.System,
            NullLogger<IAssetJobs>.Instance
        );
        _sync = Sync(config);

        _habbo.Serve(
            VARIABLES,
            $"flash.client.url=//images.habbo.com/gordon/flash-assets-PRODUCTION-1/\npet.configuration=dog,cat,dog\n"
        );
        _habbo.Serve(
            FURNIDATA,
            """
            {
              "roomitemtypes": { "furnitype": [
                { "classname": "chair", "revision": 5 },
                { "classname": "chair*2", "revision": 7 },
                { "classname": "gone", "revision": 3 },
                { "classname": "kept", "revision": 9 },
                { "classname": "lamp", "revision": 2 },
                { "classname": "ads_cheetos", "revision": 4 },
                { "classname": "bad name", "revision": 1 }
              ] },
              "wallitemtypes": { "furnitype": [
                { "classname": "poster", "revision": 1 }
              ] }
            }
            """
        );
        _habbo.Serve($"{FURNI}/7/chair.swf", NitroConverterTests.Swf());
        _habbo.Serve($"{FURNI}/2/lamp.swf", "<html><body>Access denied</body></html>");
        _habbo.Serve($"{FURNI}/4/ads_cheetos.swf", NitroConverterTests.Swf());
        _habbo.Serve($"{FURNI}/4/ads_cheetos_camp.swf", NitroConverterTests.Swf());
        // Posters 5 and 12 have texts; only 5 has a file, as some of Habbo's don't.
        _habbo.Serve(
            TEXTS,
            "poster_5_name=Five\nposter_5_desc=The fifth\nposter_12_desc=Twelve\nposterize=no\n"
        );
        _habbo.Serve($"{FURNI}/1/poster5.swf", NitroConverterTests.Swf());
        _habbo.Serve($"{FURNI}/9/kept.swf", NitroConverterTests.Swf());
        _habbo.Serve(
            $"{GORDON}/effectmap.xml",
            """
            <map>
              <effect id="1" lib="Dance1" type="dance" revision="2"/>
              <effect id="5" lib="Dance1" type="fx" revision="3"/>
              <effect id="9" lib="fx_9" type="fx" revision="1"/>
            </map>
            """
        );
        _habbo.Serve(
            $"{GORDON}/figuremap.xml",
            """
            <map>
              <lib id="hh_human_body" revision="4"><part id="1" type="bd"/></lib>
              <lib id="shirt_U_gone" revision="2"><part id="9" type="ch"/></lib>
              <lib id="hh_pets" revision="1"/>
              <lib id="hh_human_fx" revision="1"/>
            </map>
            """
        );
        _habbo.Serve($"{GORDON}/hh_human_body.swf", NitroConverterTests.Swf());
        _habbo.Serve($"{GORDON}/hh_pets.swf", NitroConverterTests.Swf());
        _habbo.Serve($"{GORDON}/Dance1.swf", NitroConverterTests.Swf());
        _habbo.Serve($"{GORDON}/dog.swf", NitroConverterTests.Swf());
        _habbo.Serve($"{GORDON}/cat.swf", NitroConverterTests.Swf());

        // Staff uploaded their own "kept"; Habbo's must never replace it.
        _db.Insert(
            new AssetBundleEntity
            {
                Kind = AssetBundleKind.Furniture,
                Name = "kept",
                Source = AssetBundleSource.Upload,
                Hash = "own",
                Size = 3,
                UpdatedAt = DateTime.UtcNow,
            }
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _folder.Dispose();
        _db.Dispose();
        _habbo.Dispose();
    }

    [Fact]
    public async Task A_sync_converts_what_Habbo_has_and_the_next_one_takes_only_what_may_pass_now()
    {
        var first = await RunAsync();

        first.Status.Should().Be(AssetJobStatus.Done, first.Error);
        // The six room libraries this Habbo lacks are among the failed, kept as ones it has not.
        first.Result.Should().Be("8 converted, 11 failed, 0 up to date, 1 uploads kept");
        first.Phase.Should().Be("Client libraries");

        var rows = await RowsAsync();

        // Furniture at the highest revision any of its colours has, kept as a bundle that reads.
        var chair = rows[(AssetBundleKind.Furniture, "chair")];
        chair.Revision.Should().Be("7");
        chair.Source.Should().Be(AssetBundleSource.Habbo);
        chair.Error.Should().BeNull();
        var chairFile = await File.ReadAllBytesAsync(
            Path.Combine(_folder.ContentRoot, "assets", "bundled", "furniture", "chair.nitro"),
            Ct
        );
        NitroBundle.Read(chairFile).Files.Keys.Should().Contain("test_box.json");
        chair.Hash.Should().Be(AssetBundleStore.HashOf(chairFile));
        chair.Size.Should().Be(chairFile.Length);

        // A 404 won't pass until the revision changes; a page from the filter may.
        rows[(AssetBundleKind.Furniture, "gone")]
            .Should()
            .Match<AssetBundleEntity>(x =>
                x.Error == "Habbo has no file at its address." && !x.Retry && x.Hash == null
            );
        rows[(AssetBundleKind.Furniture, "lamp")]
            .Should()
            .Match<AssetBundleEntity>(x => x.Error != null && x.Retry && x.Hash == null);

        // The one poster item has no file: each poster its texts name is a bundle of its own, at
        // the poster item's revision, as the client loads it.
        rows.Should().NotContainKey((AssetBundleKind.Furniture, "poster"));
        _habbo.Requests.Should().NotContain($"{FURNI}/1/poster.swf");
        rows[(AssetBundleKind.Furniture, "poster5")]
            .Should()
            .Match<AssetBundleEntity>(x => x.Revision == "1" && x.Hash != null);
        rows[(AssetBundleKind.Furniture, "poster12")]
            .Should()
            .Match<AssetBundleEntity>(x => x.Hash == null && !x.Retry);
        rows.Should().NotContainKey((AssetBundleKind.Furniture, "posterize"));

        // A campaign's version is taken under its own name, at its furniture's revision.
        rows[(AssetBundleKind.Furniture, "ads_cheetos_camp")]
            .Should()
            .Match<AssetBundleEntity>(x => x.Revision == "4" && x.Hash != null);
        rows.Should().NotContainKey((AssetBundleKind.Furniture, "bad name"));

        // Effects by library with the effects using it; pets by name with their type.
        rows[(AssetBundleKind.Effect, "Dance1")]
            .Should()
            .Match<AssetBundleEntity>(x => x.Revision == "3" && x.Ids == "1,5" && x.Hash != null);
        File.Exists(Path.Combine(_folder.Store.Root, "bundled", "effects", "Dance1.nitro"))
            .Should()
            .BeTrue();
        rows[(AssetBundleKind.Effect, "fx_9")].Retry.Should().BeFalse();

        // Clothing by the figure map's libraries, but those drawn from elsewhere.
        rows[(AssetBundleKind.Figure, "hh_human_body")]
            .Should()
            .Match<AssetBundleEntity>(x => x.Revision == "4" && x.Hash != null && x.Ids == null);
        File.Exists(Path.Combine(_folder.Store.Root, "bundled", "figures", "hh_human_body.nitro"))
            .Should()
            .BeTrue();
        rows[(AssetBundleKind.Figure, "shirt_U_gone")].Error.Should().NotBeNull();
        rows.Should().NotContainKey((AssetBundleKind.Figure, "hh_pets"));
        rows.Should().NotContainKey((AssetBundleKind.Figure, "hh_human_fx"));
        _habbo.Requests.Should().NotContain($"{GORDON}/hh_pets.swf");
        rows[(AssetBundleKind.Pet, "dog")]
            .Should()
            .Match<AssetBundleEntity>(x =>
                x.Revision == "PRODUCTION-1" && x.Ids == "0,2" && x.Hash != null
            );
        rows[(AssetBundleKind.Pet, "cat")].Ids.Should().Be("1");
        File.Exists(Path.Combine(_folder.Store.Root, "bundled", "pet", "cat.nitro"))
            .Should()
            .BeTrue();

        // The upload is as staff left it, and Habbo's file was never asked for.
        rows[(AssetBundleKind.Furniture, "kept")]
            .Should()
            .Match<AssetBundleEntity>(x =>
                x.Source == AssetBundleSource.Upload && x.Hash == "own" && x.Revision == null
            );
        _habbo.Requests.Should().NotContain($"{FURNI}/9/kept.swf");

        _habbo.ClearRequests();

        var second = await RunAsync();

        second.Result.Should().Be("0 converted, 1 failed, 18 up to date, 1 uploads kept");
        _habbo
            .Requests.Where(x => x.EndsWith(".swf", StringComparison.Ordinal))
            .Should()
            .Equal($"{FURNI}/2/lamp.swf");
        (await RowsAsync())[(AssetBundleKind.Furniture, "chair")].Hash.Should().Be(chair.Hash);
    }

    [Fact]
    public async Task The_preview_lists_what_a_sync_would_take_without_downloading_any_of_it()
    {
        var before = await _sync.PreviewAsync(relist: false, Ct);

        _habbo.Requests.Should().NotContain(x => x.EndsWith(".swf", StringComparison.Ordinal));
        before.Revision.Should().Be("PRODUCTION-1");
        before
            .Kinds.Select(x => x.Kind)
            .Should()
            .Equal(
                AssetBundleKind.Furniture,
                AssetBundleKind.Figure,
                AssetBundleKind.Effect,
                AssetBundleKind.Pet,
                AssetBundleKind.Room,
                AssetBundleKind.Badge,
                AssetBundleKind.CatalogIcon,
                AssetBundleKind.CatalogImage,
                AssetBundleKind.Sound
            );
        // The preview asks Habbo for no numbered file: it lists what the last sync found.
        _habbo
            .Requests.Should()
            .NotContain(x =>
                x.Contains("icon_", StringComparison.Ordinal)
                || x.Contains("sound_machine", StringComparison.Ordinal)
            );
        before.Kinds.Should().OnlyContain(x => x.Listed && x.Updated == 0);
        // Everything the first sync goes on to try, and only that.
        // The six room libraries are new too.
        before.Kinds.Sum(x => x.New).Should().Be(19);

        var furniture = before.Kinds[0];

        furniture.Uploads.Should().Be(1);
        furniture
            .Items.Select(x => x.Name)
            .Should()
            .Equal(
                "ads_cheetos",
                "ads_cheetos_camp",
                "chair",
                "gone",
                "lamp",
                "poster12",
                "poster5"
            );
        furniture
            .Items.Single(x => x.Name == "chair")
            .Should()
            .Match<AssetSyncPendingItem>(x =>
                x.Reason == AssetSyncReason.New && x.HabboRevision == "7" && x.HotelRevision == null
            );

        await RunAsync();
        _habbo.ClearRequests();

        // After it, only what may pass now: the library Habbo's filter refused.
        var after = await _sync.PreviewAsync(relist: false, Ct);

        _habbo.Requests.Should().BeEmpty("the sync read Habbo's lists just now");
        after.Kinds.Sum(x => x.UpToDate).Should().Be(18);
        after
            .Kinds.SelectMany(x => x.Items)
            .Should()
            .ContainSingle()
            .Which.Should()
            .Match<AssetSyncPendingItem>(x =>
                x.Name == "lamp"
                && x.Reason == AssetSyncReason.New
                && x.HotelRevision == "2"
                && x.Error != null
            );

        // A newer revision at Habbo shows once its lists are read again.
        _habbo.Serve(
            FURNIDATA,
            """{ "roomitemtypes": { "furnitype": [ { "classname": "chair", "revision": 8 } ] } }"""
        );

        (await _sync.PreviewAsync(relist: false, Ct)).Kinds[0].Updated.Should().Be(0);

        var relisted = (await _sync.PreviewAsync(relist: true, Ct)).Kinds[0];

        relisted.Updated.Should().Be(1);
        relisted
            .Items.Should()
            .ContainSingle(x => x.Reason == AssetSyncReason.Updated)
            .Which.Should()
            .Match<AssetSyncPendingItem>(x =>
                x.Name == "chair" && x.HotelRevision == "7" && x.HabboRevision == "8"
            );
    }

    [Fact]
    public async Task The_room_libraries_badges_catalog_pictures_and_trax_samples_come_laid_out_as_nitro_studio_has_them()
    {
        const string IMAGES = "https://images.habbo.com/c_images";
        var gif = "GIF89a"u8.ToArray();

        await using (var dbCtx = _db.CreateDbContext())
        {
            dbCtx.CatalogPages.Add(
                new Turbo.Database.Entities.Catalog.CatalogPageEntity
                {
                    Id = 1,
                    Localization = "Front",
                    Icon = 1,
                    Layout = "frontpage",
                    SortOrder = 0,
                    Display = Turbo.Primitives.Catalog.Enums.CatalogPageDisplay.Regular,
                    ImageData = ["catalog_frontpage_headline", ""],
                }
            );
            await dbCtx.SaveChangesAsync(Ct);
        }

        _habbo.Serve($"{GORDON}/HabboRoomContent.swf", NitroConverterTests.Swf());
        // Texts name ACH_Test1 and a badge of their own; ACH_Test2 Habbo has but no text names.
        _habbo.Serve(
            TEXTS,
            "badge_name_ACH_Test1=Tester\nTEAM_badge_name=Team\nposter_5_name=Five\n"
        );
        _habbo.Serve($"{IMAGES}/album1584/ACH_Test1.gif", gif);
        _habbo.Serve($"{IMAGES}/album1584/ACH_Test2.gif", gif);
        _habbo.Serve($"{IMAGES}/album1584/TEAM.gif", gif);
        _habbo.Serve($"{IMAGES}/catalogue/icon_1.png", "png1");
        _habbo.Serve($"{IMAGES}/catalogue/icon_3.png", "png3");
        _habbo.Serve($"{IMAGES}/catalogue/catalog_frontpage_headline.gif", gif);
        _habbo.Serve(
            "https://images.habbo.com/dcr/hof_furni/mp3/sound_machine_sample_0.mp3",
            "mp3"
        );
        // The client loads post.it as post_it; Habbo's file keeps its own name.
        _habbo.Serve(
            FURNIDATA,
            """{ "roomitemtypes": { "furnitype": [] }, "wallitemtypes": { "furnitype": [ { "classname": "post.it", "revision": 6 } ] } }"""
        );
        _habbo.Serve($"{FURNI}/6/post.it.swf", NitroConverterTests.Swf());

        var job = await RunAsync();

        job.Status.Should().Be(AssetJobStatus.Done, job.Error);

        string[] files =
        [
            "bundled/room/HabboRoomContent.nitro",
            "bundled/furniture/post_it.nitro",
            "images/badges/ACH_Test1.gif",
            "images/badges/ACH_Test2.gif",
            "images/badges/TEAM.gif",
            "images/catalog-icons/icon_1.png",
            "images/catalog-icons/icon_3.png",
            "images/catalog/catalog_frontpage_headline.gif",
            "sounds/sound_machine_sample_0.mp3",
        ];

        foreach (var file in files)
            File.Exists(Path.Combine(_folder.Store.Root, file)).Should().BeTrue(file);

        File.ReadAllBytes(Path.Combine(_folder.Store.Root, "images/badges/TEAM.gif"))
            .Should()
            .Equal(gif);

        var rows = await RowsAsync();

        // A room library Habbo has not is kept as one it lacks, and not asked for again.
        rows[(AssetBundleKind.Room, "TileCursor")]
            .Should()
            .Match<AssetBundleEntity>(x => x.Hash == null && x.Error != null && !x.Retry);
        rows.Should().NotContainKey((AssetBundleKind.Furniture, "post.it"));

        // What was found by asking is kept: the next sync asks above it, and takes nothing again.
        _habbo.ClearRequests();
        _habbo.Serve($"{IMAGES}/catalogue/icon_4.png", "png4");

        var again = await RunAsync();

        again.Status.Should().Be(AssetJobStatus.Done, again.Error);
        File.Exists(Path.Combine(_folder.Store.Root, "images/catalog-icons/icon_4.png"))
            .Should()
            .BeTrue();
        _habbo.Requests.Should().NotContain($"{IMAGES}/catalogue/icon_1.png");
        _habbo.Requests.Should().NotContain($"{IMAGES}/album1584/TEAM.gif");
    }

    [Fact]
    public async Task A_badge_its_texts_name_in_two_casings_is_kept_once_as_the_one_Habbo_serves()
    {
        // Habbo's texts name ACH_TetraBlokDailyWinner and ACH_TetrablokDailyWinner; its host,
        // which minds case, serves one, and the hotel's names are one row whatever their case.
        _habbo.Serve(
            TEXTS,
            "badge_name_ACH_TetraBlokWin=Win\nbadge_name_ACH_TetrablokWin=Win\nposter_5_name=Five\n"
        );
        _habbo.Serve(
            "https://images.habbo.com/c_images/album1584/ACH_TetrablokWin.gif",
            "GIF89a"u8.ToArray()
        );

        var job = await RunAsync();

        job.Status.Should().Be(AssetJobStatus.Done, job.Error);

        var badges = (await RowsAsync()).Values.Where(x => x.Kind == AssetBundleKind.Badge);

        badges
            .Should()
            .ContainSingle()
            .Which.Should()
            .Match<AssetBundleEntity>(x => x.Name == "ACH_TetrablokWin.gif" && x.Hash != null);
    }

    [Fact]
    public async Task A_room_library_comes_from_habbos_html_client_and_is_taken_again_when_it_changes()
    {
        var hab = NitroConverterTests.Hab(
            ("TileCursor.json", "application/json", Encoding.UTF8.GetBytes(TileCursor)),
            ("TileCursor.png", "image/png", [1, 2, 3])
        );

        TestClientRelease.Serve(_habbo.Serve, ("local_include/TileCursor.hab", hab));
        await Client().CheckAsync(wait: true, Ct);

        var job = await RunAsync();

        job.Status.Should().Be(AssetJobStatus.Done, job.Error);
        // Read from the release, not gordon; the others the client lacks still come from gordon.
        _habbo.Requests.Should().NotContain($"{GORDON}/TileCursor.swf");
        _habbo.Requests.Should().Contain($"{GORDON}/HabboRoomContent.swf");

        var tile = (await RowsAsync())[(AssetBundleKind.Room, "TileCursor")];

        tile.Revision.Should().Be(Convert.ToHexStringLower(SHA256.HashData(hab)));
        NitroBundle
            .Read(File.ReadAllBytes(_folder.Store.FullPathOf(AssetBundleKind.Room, "TileCursor")))
            .Files["TileCursor.png"]
            .Should()
            .Equal(1, 2, 3);

        // Up to date while the release is the same.
        _habbo.ClearRequests();
        (await RunAsync()).Result.Should().StartWith("0 converted");
    }

    [Fact]
    public async Task The_avatar_and_variable_fx_libraries_are_built_from_habbos_html_client()
    {
        static (string, string, byte[]) Xml(string name, string xml) =>
            (name, "text/xml", Encoding.UTF8.GetBytes(xml));
        static (string, string, byte[]) Png(string name) =>
            (name, "image/png", new RgbaImage(2, 3, new byte[2 * 3 * 4]).EncodePng());

        var avatar = NitroConverterTests.Hab(
            Xml(
                "HabboAvatarGeometry",
                """<geometry direction="4"><camera><x>0</x><y>0</y><z>10</z></camera><avatarset id="full" main="1"><bodypart id="top"/></avatarset><type id="vertical"><bodypart id="top" x="0" y="0" z="0" radius="0.1"><item id="mco" x="0" y="0" z="0" radius="0.01" nx="0" ny="0" nz="-1" double="1"/></bodypart></type></geometry>"""
            ),
            Xml(
                "HabboAvatarPartSets",
                """<partSets><partSet><part set-type="ri" flipped-set-type="li"/></partSet><activePartSet id="figure"><activePart set-type="bd"/></activePartSet></partSets>"""
            ),
            Xml(
                "HabboAvatarFigure",
                """<figuredata><colors><palette id="1"><color id="99999" index="1" club="0" selectable="1">DDDDDD</color></palette></colors><sets><settype type="hd" paletteid="1" mand_m_0="1" mand_f_0="1" mand_m_1="0" mand_f_1="0"><set id="180" gender="U" club="0" colorable="1" selectable="1" preselectable="0" sellable="0"><part id="1" type="hd" colorable="1" index="0" colorindex="1"/></set></settype></sets></figuredata>"""
            ),
            Xml(
                "dance_sixseven_animation",
                """<animation name="dance.sixseven" desc="67"><frame><bodypart id="head" action="Default" frame="0" dx="0" dy="1" dd="0"/></frame></animation>"""
            ),
            Xml(
                "action_offset_lay",
                """<offsets><offset size="h" direction="4" x="-17" y="17" z="-0.9"/></offsets>"""
            ),
            Xml("action_offset_swim", "<offsets/>"),
            Xml(
                "HabboAvatarAnimation",
                """<animationSet><action id="Move"><part set-type="bd"><frame number="0" assetpartdefinition="wlk"/><frame number="1" assetpartdefinition="wlk" repeats="2"/></part></action></animationSet>"""
            ),
            Png("user_typing_png"),
            // A default figure's part, which the figure libraries carry.
            Png("h_std_bd_1_0_0")
        );
        var visualization = NitroConverterTests.Hab(
            Png("variablefx_icon_energy"),
            Png("pet_experience_bubble_png"),
            Xml(
                "variablefx_icon_metadata",
                """<icons><icon name="energy" x="2" y="-1" /><!-- <icon name="gone" x="0" y="0" /> --></icons>"""
            ),
            Xml(
                "variablefx_renderer_mapping",
                """<variableFxRenderers><renderer id="0" name="classic_progress" rendererClass="ClassicProgressBarRenderer" /></variableFxRenderers>"""
            )
        );

        TestClientRelease.Serve(
            _habbo.Serve,
            ($"generated/{ClientBundleBuilder.AVATAR_LIBRARY}.hab", avatar),
            ($"generated/{ClientBundleBuilder.VISUALIZATION_LIBRARY}.hab", visualization)
        );
        _habbo.Serve(
            $"{GORDON}/HabboAvatarActions.xml",
            """<?xml version="1.0"?><actions><!-- Main --><action id="Lay" state="lay" precedence="100" main="1" prevents="fx.1,fx.2"><type id="1" animated="true"/></action><action id="Wave" state="wave" animation="1"><param id="default" value="1"/></action></actions>"""
        );
        await Client().CheckAsync(wait: true, Ct);

        var job = await RunAsync();

        job.Status.Should().Be(AssetJobStatus.Done, job.Error);

        var rows = await RowsAsync();
        IReadOnlyDictionary<string, byte[]> Files(string library) =>
            NitroBundle
                .Read(
                    File.ReadAllBytes(_folder.Store.FullPathOf(AssetBundleKind.Template, library))
                )
                .Files;
        JsonNode Json(string library, string file) => JsonNode.Parse(Files(library)[file])!;

        rows[(AssetBundleKind.Template, ClientBundleBuilder.AVATAR_LIBRARY)]
            .Source.Should()
            .Be(AssetBundleSource.Built);

        // The renderer's tables, and the additions under the names it asks for them by.
        var library = ClientBundleBuilder.AVATAR_LIBRARY;

        Json(library, $"{library}.json")["assets"]!
            .AsArray()
            .Select(x => x!["name"]!.GetValue<string>())
            .Should()
            .Equal("avatar_addition_user_typing");
        Json(library, $"{library}_spritesheet.json")["frames"]!["avatar_addition_user_typing"]![
            "frame"
        ]!["w"]!
            .GetValue<int>()
            .Should()
            .Be(2);
        Json(library, "geometry.json")["types"]![0]!["bodyParts"]![0]!["items"]![0]!["double"]!
            .GetValue<bool>()
            .Should()
            .BeTrue();
        Json(library, "part-sets.json")["partSet"]![0]!["flippedSetType"]!
            .GetValue<string>()
            .Should()
            .Be("li");
        Json(library, "figure-data.json")["setTypes"]![0]!["sets"]![0]!["id"]!
            .GetValue<int>()
            .Should()
            .Be(180);
        Json(library, "built-in-animations.json")[0]!["name"]!
            .GetValue<string>()
            .Should()
            .Be("dance.sixseven");
        Json(library, "action-offsets.json")[0]!["offsets"]![0]!["z"]!
            .GetValue<double>()
            .Should()
            .Be(-0.9);
        Json(library, "action-offsets.json")[1]!["action"]!.GetValue<string>().Should().Be("swim");
        Json(library, "animations.json")[0]!["parts"]![0]!["frames"]![1]!["repeats"]!
            .GetValue<int>()
            .Should()
            .Be(2);

        // Habbo's actions, without which an avatar only stands.
        var actions = Json(library, "actions.json")["actions"]!;

        actions[0]!
            .ToJsonString()
            .Should()
            .Be(
                """{"id":"Lay","state":"lay","precedence":100,"main":true,"prevents":["fx.1","fx.2"],"types":[{"id":1,"animated":true}]}"""
            );
        actions[1]!["params"]![0]!["value"]!.GetValue<string>().Should().Be("1");

        // The Variable FX bitmaps, and the tables they are drawn by.
        library = ClientBundleBuilder.VISUALIZATION_LIBRARY;
        Json(library, $"{library}.json")["assets"]!
            .AsArray()
            .Select(x => x!["name"]!.GetValue<string>())
            .Should()
            .Equal("variablefx_icon_energy");
        Json(library, ClientBundleBuilder.VARIABLE_FX_TABLES)
            .ToJsonString()
            .Should()
            .Be(
                """{"icons":{"energy":{"x":2,"y":-1}},"renderers":[{"id":0,"name":"classic_progress","rendererClass":"ClassicProgressBarRenderer"}]}"""
            );

        // Built once while the release and the actions are the same; kept when the actions can't be read.
        var built = rows[(AssetBundleKind.Template, ClientBundleBuilder.AVATAR_LIBRARY)].UpdatedAt;

        _habbo.Serve($"{GORDON}/HabboAvatarActions.xml", "<html>filtered</html>");
        (await RunAsync()).Status.Should().Be(AssetJobStatus.Done);

        (await RowsAsync())[(AssetBundleKind.Template, ClientBundleBuilder.AVATAR_LIBRARY)]
            .UpdatedAt.Should()
            .Be(built);
        Files(ClientBundleBuilder.AVATAR_LIBRARY).Should().ContainKey("actions.json");
    }

    [Fact]
    public async Task Each_library_with_a_layout_gets_its_window_templates_built_from_habbos_html_client()
    {
        static (string, string, byte[]) Xml(string name, string xml) =>
            (name, "text/xml", Encoding.UTF8.GetBytes(xml));
        static (string, string, byte[]) Png(string name, int width) =>
            (name, "image/png", new RgbaImage(width, 2, new byte[width * 2 * 4]).EncodePng());

        var windowManager = NitroConverterTests.Hab(
            Xml(
                "habbo_element_description_xml",
                """<elements><window type="frame" style="0" window_layout="habbo_window_layout_frame_xml"/><window type="scrollable_itemlist_vertical" style="0" window_layout="habbo_window_layout_list_xml"/></elements>"""
            ),
            Xml(
                "habbo_window_layout_frame_xml",
                """<layout name="frame" width="100" height="80"><window><container width="100" height="80"><children><container name="content_area" x="6" y="30" width="88" height="44"/></children></container></window></layout>"""
            ),
            Xml(
                "habbo_window_layout_list_xml",
                """<layout name="list" width="40" height="40"><window><itemlist_vertical name="_ITEMLIST" width="30" height="40"/></window></layout>"""
            ),
            Png("frame_close_png", 3)
        );
        var toolbar = NitroConverterTests.Hab(
            Xml(
                "purse_xml",
                """<?xml version="1.0"?><!-- the purse --><layout name="purse" width="120" height="60"><window><frame name="purse_frame" caption="%24%7Bpurse.title%7D" params="1" color="0xff418db0" width="120" height="60"><variables><var key="margin_top" value="20" type="int"/></variables><params><param name="relative_horizontal_scale_strech"/></params><children><bitmap name="coin"><variables><var key="asset_uri" value="coin_icon"/></variables></bitmap><bitmap name="close"><variables><var key="asset_uri" value="frame_close"/></variables></bitmap><bitmap name="gone"><variables><var key="asset_uri" value="no_such_bitmap"/></variables></bitmap><scrollable_itemlist_vertical name="list" width="40" height="40"/></children></frame></window></layout>"""
            ),
            Xml(
                "broken_xml",
                """<layout name="broken"><window><container><params><param name="no_such_param"/></params></container></window></layout>"""
            ),
            Xml("not_a_layout_xml", "<manifest/>"),
            Png("coin_icon_png", 4),
            // Drawn by name by the client's views, though no layout names it.
            Png("loose_art_png", 5)
        );
        // A library with no layout has no templates.
        var plain = NitroConverterTests.Hab(Png("art_png", 2));

        TestClientRelease.Serve(
            _habbo.Serve,
            ("generated/habbo-window-manager-com.hab", windowManager),
            ("generated/habbo-toolbar-com.hab", toolbar),
            ("generated/habbo-plain-com.hab", plain)
        );
        await Client().CheckAsync(wait: true, Ct);

        var job = await RunAsync();

        job.Status.Should().Be(AssetJobStatus.Done, job.Error);

        var rows = await RowsAsync();
        IReadOnlyDictionary<string, byte[]> Files(string library) =>
            NitroBundle
                .Read(
                    File.ReadAllBytes(_folder.Store.FullPathOf(AssetBundleKind.Template, library))
                )
                .Files;

        rows.Keys.Where(x => x.Item1 == AssetBundleKind.Template)
            .Select(x => x.Item2)
            .Should()
            .BeEquivalentTo("habbo-toolbar-com", "habbo-window-manager-com");
        rows[(AssetBundleKind.Template, "habbo-toolbar-com")]
            .Source.Should()
            .Be(AssetBundleSource.Built);

        var toolbarFiles = Files("habbo-toolbar-com");
        var templates = JsonNode.Parse(toolbarFiles["templates.json"])!;

        templates["library"]!.GetValue<string>().Should().Be("habbo-toolbar-com");
        templates["version"]!.GetValue<string>().Should().Be(TestClientRelease.VERSION);
        templates["templates"]!
            .AsObject()
            .Select(x => x.Key)
            .Should()
            .Equal("habbo-toolbar-com/purse_xml");

        var purse = templates["templates"]!["habbo-toolbar-com/purse_xml"]!;
        var frame = purse["elements"]![0]!;

        frame["caption"]!.GetValue<string>().Should().Be("${purse.title}");
        frame["color"]!.GetValue<uint>().Should().Be(0xff418db0);
        frame["params"]!
            .ToJsonString()
            .Should()
            .Be("""{"events":["input"],"scale":["stretch","fixed"]}""");
        // The frame's content area is its window layout's, the top under its own margin_top.
        frame["margins"]!.ToJsonString().Should().Be("[6,20,6,6]");

        var children = frame["children"]!.AsArray();

        // A bitmap is named after the library it is found in: its own, else the window manager's.
        children[0]!["vars"]!["asset_uri"]!
            .GetValue<string>()
            .Should()
            .Be("habbo-toolbar-com-coin_icon");
        children[1]!["vars"]!["asset_uri"]!
            .GetValue<string>()
            .Should()
            .Be("habbo-window-manager-com-frame_close");
        children[2]!["vars"]!["asset_uri"]!.GetValue<string>().Should().Be("no_such_bitmap");
        // The composite window carries the window layout it is built from.
        purse["skins"]!["scrollable_itemlist_vertical:0"]!["name"]!
            .GetValue<string>()
            .Should()
            .Be("list");

        toolbarFiles
            .Keys.Should()
            .BeEquivalentTo(
                "habbo-toolbar-com.json",
                "templates.json",
                "habbo-toolbar-com-coin_icon.png",
                "habbo-toolbar-com-loose_art.png"
            );
        Files("habbo-window-manager-com")
            .Should()
            .ContainKey("habbo-window-manager-com-frame_close.png");
        job.Log.Should()
            .Contain("Template problem: habbo-toolbar-com: no_such_bitmap")
            .And.Contain(x =>
                x.StartsWith(
                    "Template problem: habbo-toolbar-com: broken_xml (Unknown window parameter",
                    StringComparison.Ordinal
                )
            );

        // A release without the library takes its bundle away.
        await Client().ForgetAsync(TestClientRelease.VERSION, Ct);
        TestClientRelease.Serve(
            _habbo.Serve,
            ("generated/habbo-window-manager-com.hab", windowManager)
        );
        await Client().CheckAsync(wait: true, Ct);
        (await RunAsync()).Status.Should().Be(AssetJobStatus.Done);

        (await RowsAsync())
            .Keys.Where(x => x.Item1 == AssetBundleKind.Template)
            .Select(x => x.Item2)
            .Should()
            .Equal("habbo-window-manager-com");
        File.Exists(_folder.Store.FullPathOf(AssetBundleKind.Template, "habbo-toolbar-com"))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task The_window_managers_bundle_carries_the_ui_theme_beside_its_bitmaps()
    {
        TestClientRelease.Serve(
            _habbo.Serve,
            ("generated/habbo-window-manager-com.hab", Gamedata.ClientThemeTests.WindowManagerHab())
        );
        await Client().CheckAsync(wait: true, Ct);

        var job = await RunAsync();

        job.Status.Should().Be(AssetJobStatus.Done, job.Error);

        var files = NitroBundle
            .Read(
                File.ReadAllBytes(
                    _folder.Store.FullPathOf(AssetBundleKind.Template, "habbo-window-manager-com")
                )
            )
            .Files;
        var assets = JsonNode.Parse(files["habbo-window-manager-com.json"])!["assets"]!
            .AsArray()
            .Select(x => x!["name"]!.GetValue<string>())
            .ToList();

        files
            .Should()
            .ContainKeys(
                "theme.png",
                "theme_spritesheet.json",
                "theme-variants.json",
                "templates.json"
            );
        // The library's own bitmaps, and the theme's sprites, each on the sheet under its name.
        assets.Should().Contain("habbo-window-manager-com-habbo_border");
        assets.Where(x => x.StartsWith("theme-", StringComparison.Ordinal)).Should().NotBeEmpty();

        var frames = JsonNode.Parse(files["theme_spritesheet.json"])!["frames"]!.AsObject();

        assets
            .Where(x => x.StartsWith("theme-", StringComparison.Ordinal))
            .Should()
            .OnlyContain(x => frames.ContainsKey(x));
        JsonNode.Parse(files["theme-variants.json"])!["variants"].Should().NotBeNull();
    }

    private const string TileCursor = """
        {"name":"TileCursor","type":"tile_cursor","documentClass":"TileCursor","logicType":"tile_cursor",
         "assets":{"tile_cursor_64_a_0_0":{"x":1,"y":2}},
         "spritesheet":{"frames":{"TileCursor_tile_cursor_64_a_0_0":{"frame":{"x":0,"y":0,"w":1,"h":1}}},"meta":{"image":"old.png"}}}
        """;

    [Fact]
    public async Task A_bundle_whose_file_is_gone_is_downloaded_again()
    {
        await RunAsync();

        // Its row still says it was taken at Habbo's revision; the folder lost the file.
        var chair = _folder.Store.FullPathOf(AssetBundleKind.Furniture, "chair");

        File.Delete(chair);
        _habbo.ClearRequests();

        var job = await RunAsync();

        job.Status.Should().Be(AssetJobStatus.Done, job.Error);
        _habbo.Requests.Should().Contain($"{FURNI}/7/chair.swf");
        File.Exists(chair).Should().BeTrue();
    }

    [Fact]
    public async Task A_newer_revision_is_taken_again_and_the_ids_follow_the_effect_map()
    {
        await RunAsync();

        _habbo.Serve(
            FURNIDATA,
            """{ "roomitemtypes": { "furnitype": [ { "classname": "chair", "revision": 8 } ] } }"""
        );
        _habbo.Serve($"{FURNI}/8/chair.swf", NitroConverterTests.Swf(twoAlike: true));
        _habbo.Serve(
            $"{GORDON}/effectmap.xml",
            """<map><effect id="1" lib="Dance1" revision="3"/><effect id="6" lib="Dance1" revision="3"/></map>"""
        );
        _habbo.ClearRequests();

        var job = await RunAsync();

        job.Status.Should().Be(AssetJobStatus.Done, job.Error);
        _habbo.Requests.Should().Contain($"{FURNI}/8/chair.swf");
        _habbo.Requests.Should().NotContain($"{GORDON}/Dance1.swf");

        var rows = await RowsAsync();

        rows[(AssetBundleKind.Furniture, "chair")].Revision.Should().Be("8");
        rows[(AssetBundleKind.Effect, "Dance1")].Ids.Should().Be("1,6");
    }

    [Fact]
    public async Task A_check_starts_a_sync_only_when_that_is_on_and_no_job_runs()
    {
        var off = new AssetBundleConfig { SyncAfterCheck = false };

        Sync(off).StartAfterCheck(PlayerId.Parse(1)).Should().BeNull();
        _jobs.Current.Should().BeNull();
        _habbo.Requests.Should().BeEmpty();

        var hold = new TaskCompletionSource();
        _habbo.Hold = hold.Task;

        var started = _sync.StartAfterCheck(PlayerId.Parse(1));

        started.Should().NotBeNull();
        started!.Kind.Should().Be(AssetJobKind.Sync);
        _sync.StartAfterCheck(PlayerId.Parse(1)).Should().BeNull();
        _jobs.Current!.Id.Should().Be(started.Id);

        hold.SetResult();
        await WaitAsync();
    }

    private AssetSyncService Sync(AssetBundleConfig config) =>
        new(
            _db,
            Options.Create(config),
            Options.Create(new GamedataConfig()),
            new HabboGamedataClient(
                _fakes.Create<IHttpClientFactory>(),
                NullLogger<HabboGamedataClient>.Instance
            ),
            Client(),
            new ClientBundleBuilder(
                _db,
                _folder.Store,
                Client(),
                TimeProvider.System,
                NullLogger<ClientBundleBuilder>.Instance
            ),
            _folder.Store,
            _jobs,
            TimeProvider.System,
            NullLogger<IAssetSyncService>.Instance
        );

    private HabboClientService Client() =>
        new(
            _fakes.Create<IHttpClientFactory>(),
            _folder.Store,
            Options.Create(new GamedataConfig()),
            TimeProvider.System,
            NullLogger<HabboClientService>.Instance
        );

    private async Task<AssetJobSnapshot> RunAsync()
    {
        _sync.Start(PlayerId.Parse(1));

        return await WaitAsync();
    }

    private async Task<AssetJobSnapshot> WaitAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (_jobs.Running)
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "the sync should have finished");
            await Task.Delay(10, Ct);
        }

        return _jobs.Current!;
    }

    private async Task<Dictionary<(AssetBundleKind, string), AssetBundleEntity>> RowsAsync()
    {
        await using var db = _db.CreateDbContext();

        var rows = await db
            .AssetBundles.AsNoTracking()
            .ToDictionaryAsync(x => (x.Kind, x.Name), Ct);
        var entity = db.Model.FindEntityType(typeof(AssetBundleEntity))!;

        // SQLite keeps a string of any length; the hotel's MySQL refuses one longer than its column.
        foreach (var row in rows.Values)
        foreach (var property in entity.GetProperties())
            if (
                property.GetMaxLength() is { } max
                && property.PropertyInfo?.GetValue(row) is string text
            )
                text.Length.Should().BeLessThanOrEqualTo(max, $"{row.Name}'s {property.Name}");

        return rows;
    }

    /// <summary>Habbo's hosts: what it serves by address, 404 for the rest; each request recorded.</summary>
    private sealed class HabboHost : HttpMessageHandler
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();
        private readonly ConcurrentQueue<string> _requests = new();

        /// <summary>When set, every request waits for it.</summary>
        public Task? Hold { get; set; }

        public IReadOnlyList<string> Requests => [.. _requests];

        public void Serve(string url, string text) => Serve(url, Encoding.UTF8.GetBytes(text));

        public void Serve(string url, byte[] data) => _files[url] = data;

        public void ClearRequests() => _requests.Clear();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var url = request.RequestUri!.ToString();

            _requests.Enqueue(url);

            if (Hold is { } hold)
                await hold.WaitAsync(cancellationToken);

            return _files.TryGetValue(url, out var data)
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(data),
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
