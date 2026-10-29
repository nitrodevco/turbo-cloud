using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Database.Context;
using Turbo.Database.Entities.Assets;
using Turbo.Gamedata;
using Turbo.Gamedata.Assets;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.Habbo.Client;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Assets;

public sealed class AssetJobShutdownTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Host_shutdown_waits_for_asset_sync_final_flush()
    {
        var pause = new SavePause();
        using var db = new SqliteDb(pause);
        using var folder = new AssetFolder(db);
        using var remote = new Remote();
        var fakes = new Fakes();
        fakes.Handlers["CreateClient"] = _ => new HttpClient(remote, disposeHandler: false);
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { ContentRootPath = folder.ContentRoot }
        );
        builder.Services.AddSingleton<IDbContextFactory<TurboDbContext>>(db);
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.Configure<AssetBundleConfig>(_ => { });
        builder.Services.Configure<GamedataConfig>(_ => { });
        builder.Services.AddSingleton(fakes.Create<IHttpClientFactory>());
        builder.Services.AddSingleton(fakes.Create<IChatStyleService>());
        builder.Services.AddSingleton<HabboGamedataClient>();
        builder.Services.AddSingleton<HabboClientService>();
        builder.Services.AddSingleton<ClientBundleBuilder>();
        builder.Services.AddSingleton<AssetDiscoveryService>();
        builder.Services.AddSingleton(_ => pause);
        builder.Services.AddAssetBundleServices();
        using (var host = builder.Build())
        {
            await host.StartAsync(Ct);
            host.Services.GetRequiredService<SavePause>();
            var jobs = host.Services.GetRequiredService<IAssetJobs>();
            host.Services.GetRequiredService<IAssetSyncService>().Start(PlayerId.Parse(1));
            Task? stopping = null;
            try
            {
                await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
                var store = host.Services.GetRequiredService<IAssetBundleStore>();
                var bytes = await File.ReadAllBytesAsync(
                    Path.Combine(store.Root, store.PathOf(AssetBundleKind.Furniture, "chair")),
                    Ct
                );
                bytes.Should().NotBeEmpty();
                stopping = host.StopAsync(Ct);
                host.Services.GetRequiredService<IHostApplicationLifetime>()
                    .ApplicationStopping.IsCancellationRequested.Should()
                    .BeTrue();
                stopping
                    .IsCompleted.Should()
                    .BeFalse("the completed file still needs its metadata save");
                host.Services.GetServices<IHostedService>()
                    .OfType<AssetJobs>()
                    .Should()
                    .ContainSingle()
                    .Which.Should()
                    .BeSameAs(jobs);
                jobs.Running.Should().BeTrue();
                pause.Disposed.Should().BeFalse();
                var start = () =>
                    host.Services.GetRequiredService<IAssetSyncService>().Start(PlayerId.Parse(2));
                start.Should().Throw<InvalidOperationException>().WithMessage("*stopping*");
                pause.Release.TrySetResult();
                await stopping.WaitAsync(TimeSpan.FromSeconds(10), Ct);
                jobs.Current!.Status.Should().Be(AssetJobStatus.Canceled);
                pause.Disposed.Should().BeFalse();
                await using var context = await db.CreateDbContextAsync(Ct);
                var row = await context.AssetBundles.SingleAsync(
                    x => x.Kind == AssetBundleKind.Furniture && x.Name == "chair",
                    Ct
                );
                row.Hash.Should().Be(GamedataBytes.Hash(bytes));
                row.Size.Should().Be(bytes.Length);
                row.Error.Should().BeNull();
            }
            finally
            {
                pause.Release.TrySetResult();
                await (stopping ?? host.StopAsync(Ct)).WaitAsync(TimeSpan.FromSeconds(10), Ct);
            }
        }
        pause.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Stopping_rejects_jobs_even_before_the_hosted_stop_callback()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.Configure<AssetBundleConfig>(_ => { });
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<IAssetJobs, AssetJobs>();
        using var host = builder.Build();
        await host.StartAsync(Ct);
        var jobs = host.Services.GetRequiredService<IAssetJobs>();
        host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        var start = () =>
            jobs.Start(
                AssetJobKind.Sync,
                "fixture",
                PlayerId.Parse(1),
                (_, _) => Task.FromResult("done")
            );
        start.Should().Throw<InvalidOperationException>().WithMessage("*stopping*");
        jobs.Current.Should().BeNull();
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task A_stop_deadline_bounds_the_wait_but_a_later_stop_still_joins_the_job()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.Configure<AssetBundleConfig>(_ => { });
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<AssetJobs>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<AssetJobs>());
        using var host = builder.Build();
        await host.StartAsync(Ct);
        var jobs = host.Services.GetRequiredService<AssetJobs>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        jobs.Start(
            AssetJobKind.Sync,
            "fixture",
            PlayerId.Parse(1),
            async (_, token) =>
            {
                using var registration = token.Register(() => canceled.TrySetResult());
                entered.TrySetResult();
#pragma warning disable VSTHRD003 // The test owns the release signal and completes it in finally.
                await release.Task.WaitAsync(Ct);
#pragma warning restore VSTHRD003
                token.ThrowIfCancellationRequested();
                return "done";
            }
        );
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            using var deadline = new CancellationTokenSource();
            var stop = ((IHostedService)jobs).StopAsync(deadline.Token);
            await canceled.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            await deadline.CancelAsync();
#pragma warning disable VSTHRD003 // This is the pool-backed stop task started by this test.
            var timedOut = () => stop;
#pragma warning restore VSTHRD003
            await timedOut.Should().ThrowAsync<OperationCanceledException>();
            jobs.Running.Should().BeTrue();
            var joining = ((IHostedService)jobs).StopAsync(Ct);
            joining.IsCompleted.Should().BeFalse();
            release.TrySetResult();
            await joining.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            jobs.Current!.Status.Should().Be(AssetJobStatus.Canceled);
        }
        finally
        {
            release.TrySetResult();
            await host.StopAsync(Ct).WaitAsync(TimeSpan.FromSeconds(10), Ct);
        }
    }

    private sealed class SavePause : SaveChangesInterceptor, IDisposable
    {
        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                eventData
                    .Context!.ChangeTracker.Entries<AssetBundleEntity>()
                    .Any(x => x.Entity.Name == "chair")
            )
            {
                cancellationToken.CanBeCanceled.Should().BeFalse();
                Entered.TrySetResult();
                await Release.Task.WaitAsync(Ct);
                Disposed
                    .Should()
                    .BeFalse("the host must drain the save before disposing dependencies");
            }
            return result;
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class Remote : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var url = request.RequestUri!.ToString();
            if (url == "https://images.habbo.com/dcr/hof_furni/1/chair.swf")
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(NitroConverterTests.Swf()),
                    }
                );
            var content = url switch
            {
                "https://www.habbo.com/gamedata/external_variables/0" =>
                    "flash.client.url=//images.habbo.com/gordon/flash-assets-PRODUCTION-1/\n",
                "https://www.habbo.com/gamedata/furnidata_json/0" =>
                    """{"roomitemtypes":{"furnitype":[{"classname":"chair","revision":1}]},"wallitemtypes":{"furnitype":[]}}""",
                "https://www.habbo.com/gamedata/external_flash_texts/0" => "",
                _ => null,
            };
            return Task.FromResult(
                content is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(content),
                    }
            );
        }
    }
}
