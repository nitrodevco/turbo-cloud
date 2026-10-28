using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

public sealed class AssetDiscoveryLifecycleTests : IAsyncDisposable
{
    private const string VARIABLES = "https://www.habbo.com/gamedata/external_variables/0";
    private const string ROOM_PATH = "local_include/HabboRoomContent.hab";
    private readonly CatalogPause _pause = new();
    private readonly SqliteDb _db;
    private readonly AssetFolder _folder;
    private readonly Remote _remote = new();
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _clock = new(ManualTimeProvider.FixedStart);
    private readonly Lifetime _lifetime = new();
    private readonly CapturingLogger<AssetDiscoveryCatalog> _log = new(keepExceptions: false);
    private readonly HabboClientService _client;
    private readonly AssetDiscoveryService _discovery;
    private readonly AssetDiscoveryCatalog _catalog;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AssetDiscoveryLifecycleTests()
    {
        _db = new SqliteDb(_pause);
        _folder = new AssetFolder(_db);
        _fakes.Handlers["CreateClient"] = _ => new HttpClient(_remote, disposeHandler: false);
        _client = new HabboClientService(
            _fakes.Create<IHttpClientFactory>(),
            _folder.Store,
            Options.Create(new GamedataConfig()),
            TimeProvider.System,
            NullLogger<HabboClientService>.Instance
        );
        _discovery = new AssetDiscoveryService(
            _db,
            Options.Create(new AssetBundleConfig()),
            Options.Create(new GamedataConfig()),
            Habbo(),
            _client,
            _folder.Store,
            _clock,
            NullLogger<AssetDiscoveryService>.Instance
        );
        _catalog = new AssetDiscoveryCatalog(
            _discovery,
            Options.Create(new AssetBundleConfig { SyncPreviewMinutes = 1 }),
            _clock,
            _lifetime,
            _log
        );
    }

    public async ValueTask DisposeAsync()
    {
        _pause.Release.TrySetResult();
        _remote.Release.TrySetResult();
        await _catalog.DisposeAsync();
        _folder.Dispose();
        _db.Dispose();
        _remote.Dispose();
        _lifetime.Dispose();
    }

    [Fact]
    public async Task Overlapping_discovery_keeps_each_room_hash_bound_to_its_own_client_release()
    {
        var bytes = "HAB fixture room"u8.ToArray();
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var root = Path.Combine(_folder.Store.Root, ".habbo-client");
        var release = Path.Combine(root, "releases", "release-a");
        Directory.CreateDirectory(release);
        var asar = TestClientRelease.Asar(
            ("release-manifest.json", TestClientRelease.Manifest("release-a", (ROOM_PATH, bytes))),
            (ROOM_PATH, bytes)
        );
        await File.WriteAllBytesAsync(Path.Combine(release, "app.asar"), asar, Ct);
        await File.WriteAllTextAsync(
            Path.Combine(release, "release.json"),
            JsonSerializer.Serialize(
                new
                {
                    Version = "release-a",
                    ReleaseId = "release-a",
                    Url = "fixture",
                    DownloadedAt = DateTime.UnixEpoch,
                    Environments = new[] { "production" },
                    Size = asar.Length,
                    Files = new[]
                    {
                        new
                        {
                            Path = ROOM_PATH,
                            Size = bytes.Length,
                            Sha256 = sha,
                        },
                    },
                }
            ),
            Ct
        );
        await File.WriteAllTextAsync(
            Path.Combine(root, "environments.json"),
            "{ \"production\": { \"Version\": \"release-a\" } }",
            Ct
        );
        _pause.Enabled = true;
        var first = _discovery.DiscoverAsync(_ => { }, probe: false, Ct);
        try
        {
            await _pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            // The first run already chose room hashes from release-a. A second run now follows none.
            await File.WriteAllTextAsync(Path.Combine(root, "environments.json"), "{}", Ct);
            var second = await _discovery.DiscoverAsync(_ => { }, probe: false, Ct);
            second.Client.Should().BeNull();
            second.Room.Single(x => x.Name == "HabboRoomContent").Download.Should().BeNull();
        }
        finally
        {
            _pause.Release.TrySetResult();
            await first;
        }
        var result = await first;
        var room = result.Room.Single(x => x.Name == "HabboRoomContent");
        room.Revision.Should().Be(sha);
        room.Download.Should().Be(HabboAssetUrls.CLIENT_PREFIX + ROOM_PATH);
        result
            .Client.Should()
            .NotBeNull("the result must keep the release that supplied its room hashes");
        result.Client!.Version.Should().Be("release-a");
        result.Client.Read(ROOM_PATH).Should().Equal(bytes);
    }

    [Fact]
    public async Task Sync_discovery_waits_for_an_active_preview_instead_of_starting_an_overlapping_read()
    {
        Func<IAssetJobProgress, CancellationToken, Task<string>>? work = null;
        _fakes.Handlers["Start"] = call =>
        {
            work = (Func<IAssetJobProgress, CancellationToken, Task<string>>)call.Args[3]!;
            return new AssetJobSnapshot
            {
                Id = Guid.NewGuid(),
                Kind = AssetJobKind.Sync,
                Title = "fixture",
                Status = AssetJobStatus.Running,
                Phase = "Listing",
                Total = 0,
                Done = 0,
                Failed = 0,
                Log = [],
                PlayerId = 1,
                StartedAt = DateTime.UtcNow,
            };
        };
        var sync = Sync();
        _remote.HoldVariables = true;
        var preview = sync.PreviewAsync(relist: false, Ct);
        await _remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        sync.Start(PlayerId.Parse(1));
        work.Should().NotBeNull();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        // Enter the captured job directly, making the point before its first await deterministic.
        var running = work!(_fakes.Create<IAssetJobProgress>(), stop.Token);
        try
        {
            _remote
                .Requests.Count(x => x == VARIABLES)
                .Should()
                .Be(1, "preview and sync must share discovery coordination");
        }
        finally
        {
            await stop.CancelAsync();
            try
            {
                await running;
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            _remote.Release.TrySetResult();
            await preview;
        }
    }

    [Fact]
    public async Task Fresh_cache_is_reused_and_forced_relist_performs_its_own_read()
    {
        var first = await _catalog.PreviewAsync(relist: false, Ct);
        var requests = _remote.Requests.Count;
        (await _catalog.PreviewAsync(relist: false, Ct)).Should().BeSameAs(first);
        _remote.Requests.Count.Should().Be(requests);
        _remote.Revision = "PRODUCTION-2";
        var forced = await _catalog.PreviewAsync(relist: true, Ct);
        forced.Revision.Should().Be("PRODUCTION-2");
        _remote.Requests.Count.Should().BeGreaterThan(requests);
        (await _catalog.PreviewAsync(relist: false, Ct)).Should().BeSameAs(forced);
    }

    [Fact]
    public async Task Stale_preview_is_immediate_and_only_one_background_read_runs()
    {
        var previous = await _catalog.PreviewAsync(relist: false, Ct);
        _clock.Advance(TimeSpan.FromMinutes(2));
        _remote.HoldVariables = true;
        _remote.Revision = "PRODUCTION-2";
        try
        {
            (await _catalog.PreviewAsync(relist: false, Ct).WaitAsync(TimeSpan.FromSeconds(10), Ct))
                .Should()
                .BeSameAs(previous);
            await _remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            (await _catalog.PreviewAsync(relist: false, Ct)).Should().BeSameAs(previous);
            _remote
                .Requests.Count(x => x == VARIABLES)
                .Should()
                .Be(2, "one initial read and one shared background refresh");
        }
        finally
        {
            _remote.Release.TrySetResult();
            await _catalog.WhenRefreshedAsync(Ct);
        }
        var refreshed = await _catalog.PreviewAsync(relist: false, Ct);
        refreshed.Revision.Should().Be("PRODUCTION-2");
        refreshed.ListedAt.Should().Be(_clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task A_failed_background_refresh_keeps_the_last_snapshot_and_a_later_visit_retries()
    {
        var previous = await _catalog.PreviewAsync(relist: false, Ct);
        _clock.Advance(TimeSpan.FromMinutes(2));
        _remote.FailVariables = true;
        (await _catalog.PreviewAsync(relist: false, Ct)).Should().BeSameAs(previous);
        await _catalog.WhenRefreshedAsync(Ct);
        _log.Entries.Should()
            .Contain(x =>
                x.Level == Microsoft.Extensions.Logging.LogLevel.Warning
                && x.Message.Contains("sync preview failed", StringComparison.Ordinal)
            );
        // Hold the retry so the failed refresh cannot be mistaken for a newly published result.
        _remote.FailVariables = false;
        _remote.Revision = "PRODUCTION-2";
        _remote.HoldVariables = true;
        (await _catalog.PreviewAsync(relist: false, Ct)).Should().BeSameAs(previous);
        try
        {
            await _remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        }
        finally
        {
            _remote.Release.TrySetResult();
            await _catalog.WhenRefreshedAsync(Ct);
        }
        (await _catalog.PreviewAsync(relist: false, Ct)).Revision.Should().Be("PRODUCTION-2");
    }

    [Fact]
    public async Task Cancelling_a_queued_request_does_not_cancel_the_active_read()
    {
        _remote.HoldVariables = true;
        var active = _catalog.PreviewAsync(relist: false, Ct);
        await _remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var queued = _catalog.PreviewAsync(relist: true, cancelled.Token);
        try
        {
            await cancelled.CancelAsync();
            var cancelledRead = () => queued.WaitAsync(Ct);
            await cancelledRead.Should().ThrowAsync<OperationCanceledException>();
            active.IsCompleted.Should().BeFalse();
            _remote.Requests.Count(x => x == VARIABLES).Should().Be(1);
        }
        finally
        {
            _remote.Release.TrySetResult();
        }
        (await active).Revision.Should().Be("PRODUCTION-1");
    }

    [Fact]
    public async Task Cancelling_the_first_cold_reader_does_not_publish_or_cancel_the_next_reader()
    {
        _remote.HoldVariables = true;
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var first = _catalog.PreviewAsync(relist: false, cancelled.Token);
        await _remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var second = _catalog.PreviewAsync(relist: false, Ct);
        await cancelled.CancelAsync();
        try
        {
            var cancelledRead = () => first.WaitAsync(Ct);
            await cancelledRead.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            _remote.Release.TrySetResult();
        }
        var completed = await second;
        completed.Revision.Should().Be("PRODUCTION-1");
        _remote.Requests.Count(x => x == VARIABLES).Should().Be(2);
        (await _catalog.PreviewAsync(relist: false, Ct)).Should().BeSameAs(completed);
    }

    [Fact]
    public async Task A_cancelled_forced_refresh_preserves_the_previous_snapshot()
    {
        var previous = await _catalog.PreviewAsync(relist: false, Ct);
        _remote.HoldVariables = true;
        _remote.Revision = "PRODUCTION-2";
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var refresh = _catalog.PreviewAsync(relist: true, cancelled.Token);
        await _remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await cancelled.CancelAsync();
        var cancelledRead = () => refresh.WaitAsync(Ct);
        await cancelledRead.Should().ThrowAsync<OperationCanceledException>();
        (await _catalog.PreviewAsync(relist: false, Ct)).Should().BeSameAs(previous);
    }

    [Fact]
    public async Task A_failed_sync_discovery_does_not_replace_the_preview_cache()
    {
        var previous = await _catalog.PreviewAsync(relist: false, Ct);
        _remote.FailVariables = true;
        var refresh = () => _catalog.ForSyncAsync(_ => { }, Ct);
        await refresh.Should().ThrowAsync<HttpRequestException>();
        (await _catalog.PreviewAsync(relist: false, Ct)).Should().BeSameAs(previous);
    }

    [Fact]
    public async Task Sync_keeps_its_probing_pass_after_waiting_for_a_nonprobing_preview()
    {
        _remote.HoldVariables = true;
        var preview = _catalog.PreviewAsync(relist: false, Ct);
        await _remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var sync = _catalog.ForSyncAsync(_ => { }, Ct);
        _remote.Release.TrySetResult();
        await preview;
        await sync;
        _remote.Requests.Count(x => x == VARIABLES).Should().Be(2);
        _remote.Requests.Should().Contain(x => x.Contains("icon_", StringComparison.Ordinal));
        _remote
            .Requests.Should()
            .Contain(x => x.Contains("sound_machine", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Host_shutdown_cancels_foreground_and_queued_reads_and_drains_before_disposal()
    {
        _remote.HoldVariables = true;
        var foreground = _catalog.PreviewAsync(relist: false, Ct);
        await _remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var queued = _catalog.PreviewAsync(relist: true, Ct);
        var sync = _catalog.ForSyncAsync(_ => { }, Ct);
        _lifetime.StopApplication();
        await _catalog.StopAsync(Ct).WaitAsync(TimeSpan.FromSeconds(10), Ct);
        foreach (var task in new[] { foreground, queued, sync })
        {
            var cancelledRead = () => task.WaitAsync(Ct);
            await cancelledRead.Should().ThrowAsync<OperationCanceledException>();
        }
        _remote.Requests.Count(x => x == VARIABLES).Should().Be(1);
        var afterStop = () => _catalog.PreviewAsync(relist: false, Ct);
        await afterStop.Should().ThrowAsync<OperationCanceledException>();
        await _catalog.StopAsync(Ct);
        await _catalog.DisposeAsync();
        await _catalog.DisposeAsync();
    }

    [Fact]
    public async Task Shutdown_joins_a_background_read_and_honors_a_callers_wait_deadline()
    {
        await _catalog.PreviewAsync(relist: false, Ct);
        _clock.Advance(TimeSpan.FromMinutes(2));
        _remote.HoldVariables = true;
        _remote.IgnoreCancellation = true;
        await _catalog.PreviewAsync(relist: false, Ct);
        await _remote.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await cancelled.CancelAsync();
        var stopping = () => _catalog.StopAsync(cancelled.Token);
        await stopping.Should().ThrowAsync<OperationCanceledException>();
        var disposal = _catalog.DisposeAsync().AsTask();
        try
        {
            disposal
                .IsCompleted.Should()
                .BeFalse("shutdown must join even an HTTP handler that ignores cancellation");
        }
        finally
        {
            _remote.Release.TrySetResult();
            await disposal.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        }
    }

    [Fact]
    public async Task Registration_exposes_the_same_singleton_as_the_hosted_service_and_disposes_synchronously()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(_discovery)
            .AddSingleton(Options.Create(new AssetBundleConfig()))
            .AddSingleton<TimeProvider>(_clock)
            .AddSingleton<IHostApplicationLifetime>(_lifetime);
        services.AddAssetBundleServices();
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<AssetDiscoveryCatalog>();
        provider
            .GetServices<IHostedService>()
            .OfType<AssetDiscoveryCatalog>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeSameAs(catalog);
        await catalog.StartAsync(Ct);
        await catalog.PreviewAsync(relist: false, Ct);
        await catalog.StopAsync(Ct);
    }

    private HabboGamedataClient Habbo() =>
        new(_fakes.Create<IHttpClientFactory>(), NullLogger<HabboGamedataClient>.Instance);

    private AssetSyncService Sync() =>
        new(
            _db,
            Options.Create(new AssetBundleConfig()),
            Options.Create(new GamedataConfig()),
            Habbo(),
            _catalog,
            new ClientBundleBuilder(
                _db,
                _folder.Store,
                _client,
                _fakes.Create<IChatStyleService>(),
                Options.Create(new GamedataConfig()),
                TimeProvider.System,
                NullLogger<ClientBundleBuilder>.Instance
            ),
            _folder.Store,
            _fakes.Create<IAssetJobs>(),
            TimeProvider.System,
            NullLogger<IAssetSyncService>.Instance
        );

    private sealed class Lifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _stopping = new();
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => _stopping.Cancel();

        public void Dispose() => _stopping.Dispose();
    }

    private sealed class CatalogPause : DbCommandInterceptor
    {
        private int _calls;
        public bool Enabled { get; set; }
        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                Enabled
                && command.CommandText.Contains("catalog_pages", StringComparison.Ordinal)
                && Interlocked.Increment(ref _calls) == 1
            )
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class Remote : HttpMessageHandler
    {
        public ConcurrentQueue<string> Requests { get; } = new();
        public bool HoldVariables { get; set; }
        public bool FailVariables { get; set; }
        public bool IgnoreCancellation { get; set; }
        public string Revision { get; set; } = "PRODUCTION-1";
        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var url = request.RequestUri!.ToString();
            Requests.Enqueue(url);
            if (url == VARIABLES && HoldVariables)
            {
                Entered.TrySetResult();
                if (IgnoreCancellation)
                    await Release.Task.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                else
                    await Release.Task.WaitAsync(cancellationToken);
            }
            if (url == VARIABLES && FailVariables)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            var content = url switch
            {
                VARIABLES =>
                    $"flash.client.url=//images.habbo.com/gordon/flash-assets-{Revision}/\n",
                "https://www.habbo.com/gamedata/furnidata_json/0" =>
                    "{ \"roomitemtypes\": { \"furnitype\": [] }, \"wallitemtypes\": { \"furnitype\": [] } }",
                "https://www.habbo.com/gamedata/external_flash_texts/0" => "",
                _ => null,
            };
            return content is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content),
                };
        }
    }
}
