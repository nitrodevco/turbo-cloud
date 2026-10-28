using System.Collections.Concurrent;
using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Hotel;
using Turbo.Operations.Grains;
using Turbo.Operations.Scheduling;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Scheduling.Grains;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Testing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins.Scheduling;

/// <summary>
/// A plugin's scheduled tasks as a hotel loads them: found by the scan, run through the scheduler
/// grain's run now, and cancelled when the plugin unloads mid-run, before the plugin's services
/// are disposed (docs/plugin-scheduler.md). The template's example job, whose SkillBook takes the
/// hotel API (IHotel) by constructor, runs the same way.
/// </summary>
public sealed class ScheduledTaskPluginTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-scheduled-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task Unloading_a_plugin_mid_run_cancels_the_run_before_its_services_go()
    {
        var events = new ConcurrentQueue<string>();
        var (scheduler, host, manager) = Hotel(services => services.AddSingleton(events));

        PluginBuilder.Write(_folder, "slow", SLOW);
        await manager.LoadAllAsync(ct: Ct);
        manager
            .Snapshot()
            .Single()
            .State.Should()
            .Be(PluginStateType.Loaded, manager.Snapshot().Single().Error);
        host.Keys.Should().Equal("slow.SlowJob");

        await host.SubscribeAsync(host, Ct);
        (await scheduler.RunNowAsync("slow.SlowJob", Ct)).Should().Be(ScheduledTaskRunNow.Started);
        await WaitForAsync(() => events.Contains("started"));

        await manager.UnloadAllAsync(Ct);

        events.Should().Equal("started", "cancelled", "services disposed");
        host.Keys.Should().BeEmpty();
        await WaitForAsync(async () =>
            (await scheduler.ListAsync(Ct)) is var tasks
            && (tasks.IsEmpty || tasks[0].LastRun is not null)
        );
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.ScheduledTaskRuns.SingleAsync(Ct))
            .Outcome.Should()
            .Be(nameof(ScheduledTaskOutcome.Cancelled));
    }

    [Fact]
    public async Task The_templates_weekly_job_is_found_and_runs_from_run_now()
    {
        var (scheduler, host, manager) = Hotel(services =>
            services.AddSingleton<IClientModuleDirectory>(_fakes.Create<IClientModuleDirectory>())
        );
        var template = Path.Combine(RepositoryRoot(), "templates", "turbo-plugin", "server");

        PluginBuilder.Write(
            _folder,
            "skills",
            [.. Directory.EnumerateFiles(template, "*.cs").Select(File.ReadAllText)]
        );
        await manager.LoadAllAsync(ct: Ct);
        manager
            .Snapshot()
            .Single()
            .State.Should()
            .Be(PluginStateType.Loaded, manager.Snapshot().Single().Error);

        await host.SubscribeAsync(host, Ct);

        var listed = (await scheduler.ListAsync(Ct)).Single();
        listed.Key.Should().Be("skills.SkillsWeeklyReset");
        listed.Description.Should().Be("Mondays 00:00");

        (await scheduler.RunNowAsync(listed.Key, Ct)).Should().Be(ScheduledTaskRunNow.Started);
        await WaitForAsync(async () =>
            (await scheduler.ListAsync(Ct)).Single().LastRun is not null
        );
        (await scheduler.ListAsync(Ct))
            .Single()
            .LastRun!.Outcome.Should()
            .Be(ScheduledTaskOutcome.Succeeded);

        await manager.UnloadAllAsync(Ct);
    }

    /// <summary>
    /// The scheduler grain (no silo: its timer is inert), this server's host reaching it through
    /// the grain factory, and a plugin manager whose scan finds scheduled tasks.
    /// </summary>
    private (ScheduledTaskGrain, ScheduledTaskHost, PluginManager) Hotel(
        Action<IServiceCollection> services
    )
    {
        var scheduler = GrainHarness.Attach(
            new ScheduledTaskGrain(
                _db,
                TimeProvider.System,
                NullLogger<IScheduledTaskGrain>.Instance
            ),
            _fakes
        );

        _fakes.Instances[(typeof(IScheduledTaskGrain), SingletonGrainId.GLOBAL)] = scheduler;

        var host = new ScheduledTaskHost(
            _fakes.Create<Orleans.IGrainFactory>(),
            TimeProvider.System,
            NullLogger<ScheduledTaskHost>.Instance
        );
        var collection = new ServiceCollection()
            .AddSingleton<IPluginStopping>(host)
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddSingleton<IHotel>(new FakeHotel());

        services(collection);

        var manager = new PluginManager(
            collection.BuildServiceProvider(),
            new AssemblyProcessor([new ScheduledTaskFeatureProcessor(host)]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );

        return (scheduler, host, manager);
    }

    private static async Task WaitForAsync(Func<Task<bool>> done)
    {
        for (var i = 0; i < 200 && !await done(); i++)
            await Task.Delay(25, Ct);

        (await done()).Should().BeTrue();
    }

    private static Task WaitForAsync(Func<bool> done) =>
        WaitForAsync(() => Task.FromResult(done()));

    private static string RepositoryRoot()
    {
        for (
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            dir is not null;
            dir = dir.Parent
        )
            if (File.Exists(Path.Combine(dir.FullName, "Turbo.Cloud.sln")))
                return dir.FullName;

        throw new InvalidOperationException("Turbo.Cloud.sln was not found above the tests.");
    }

    // A job that runs until cancelled, and a plugin service that says when it is disposed.
    private const string SLOW = """
        using System;
        using System.Collections.Concurrent;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;
        using Turbo.Contracts.Plugins.Scheduling;

        public sealed class SlowPlugin : ITurboPlugin
        {
            public string Key => "slow";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) =>
                services.AddSingleton<Tracker>();
            public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        public sealed class Tracker : IDisposable
        {
            public ConcurrentQueue<string> Events { get; set; } = new();
            public void Started() => Events.Enqueue("started");
            public void Cancelled() => Events.Enqueue("cancelled");
            public void Dispose() => Events.Enqueue("services disposed");
        }

        // The plugin's own service and the host's queue the test reads.
        [Every("01:00:00")]
        public sealed class SlowJob : IScheduledTask
        {
            private readonly Tracker tracker;

            public SlowJob(Tracker tracker, ConcurrentQueue<string> events)
            {
                this.tracker = tracker;
                tracker.Events = events;
            }

            public async Task RunAsync(ScheduledTaskRun run, CancellationToken ct)
            {
                tracker.Started();

                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    tracker.Cancelled();
                    throw;
                }
            }
        }
        """;
}
