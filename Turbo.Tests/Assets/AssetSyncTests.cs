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
            $"flash.client.url=//images.habbo.com/gordon/flash-assets-PRODUCTION-1/\npet.configuration=dog,cat\n"
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
                { "classname": "bad name", "revision": 1 }
              ] },
              "wallitemtypes": { "furnitype": [
                { "classname": "poster", "revision": 1 }
              ] }
            }
            """
        );
        _habbo.Serve($"{FURNI}/7/chair.swf", NitroConverterTests.Swf());
        _habbo.Serve($"{FURNI}/1/poster.swf", "<html><body>Access denied</body></html>");
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
        first.Result.Should().Be("5 converted, 4 failed, 0 up to date, 1 uploads kept");
        first.Phase.Should().Be("Pets");

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
        rows[(AssetBundleKind.Furniture, "poster")]
            .Should()
            .Match<AssetBundleEntity>(x => x.Error != null && x.Retry && x.Hash == null);
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
                x.Revision == "PRODUCTION-1" && x.Ids == "0" && x.Hash != null
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

        second.Result.Should().Be("0 converted, 1 failed, 8 up to date, 1 uploads kept");
        _habbo
            .Requests.Where(x => x.EndsWith(".swf", StringComparison.Ordinal))
            .Should()
            .Equal($"{FURNI}/1/poster.swf");
        (await RowsAsync())[(AssetBundleKind.Furniture, "chair")].Hash.Should().Be(chair.Hash);
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
