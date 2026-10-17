using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Assets.Nitro;
using Turbo.Database.Entities.Assets;
using Turbo.Gamedata.Assets;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
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
        first.Phase.Should().Be("Trax samples");

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
            _folder.Store,
            _jobs,
            TimeProvider.System,
            NullLogger<IAssetSyncService>.Instance
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

        return await db.AssetBundles.AsNoTracking().ToDictionaryAsync(x => (x.Kind, x.Name), Ct);
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
