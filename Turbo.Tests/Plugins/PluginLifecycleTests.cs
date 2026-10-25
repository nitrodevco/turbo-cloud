using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Plugins;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// What a plugin is given around its own start and stop (docs/extensions.md): the hotel-ready
/// hook and the stopping token, which replaced a copied <c>ApplicationStarted.Register</c> and a
/// private token source in 5 plugins of the 10-25 migration, and its own registered services
/// taking host services by constructor, which needed a <c>Host&lt;T&gt;</c> factory in 6.
/// </summary>
public sealed class PluginLifecycleTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-lifecycle-" + Guid.NewGuid().ToString("N")
    );
    private readonly ConcurrentQueue<string> _events = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task The_ready_hook_runs_once_the_hotel_starts_and_at_once_on_a_reload_until_stopping_cancels_it()
    {
        var lifetime = new Lifetime();
        var manager = Manager(services =>
            services.AddSingleton<IHostApplicationLifetime>(lifetime)
        );

        PluginBuilder.Write(_folder, "watch", WATCH);
        await manager.LoadAllAsync(ct: Ct);
        Loaded(manager);

        _events.Should().Equal(["starting"], "the hotel has not started yet");

        lifetime.Start();
        await WaitForAsync(() => _events.Contains("ready"));

        await manager.UnloadAllAsync(Ct);
        _events.Should().Equal("starting", "ready", "cancelled", "stopping");

        // A load once the hotel is up: ready at once.
        _events.Clear();
        await manager.LoadAllAsync(ct: Ct);
        await WaitForAsync(() => _events.Contains("ready"));
        await manager.UnloadAllAsync(Ct);
        _events.Should().Equal("starting", "ready", "cancelled", "stopping");
    }

    [Fact]
    public async Task A_service_the_plugin_registers_takes_host_services_by_constructor()
    {
        var manager = Manager(services => services.AddSingleton(TimeProvider.System));

        PluginBuilder.Write(_folder, "stamp", STAMP);
        await manager.LoadAllAsync(ct: Ct);
        Loaded(manager);

        _events.Should().Equal("stamped", "hosted");

        await manager.UnloadAllAsync(Ct);
    }

    private PluginManager Manager(Action<IServiceCollection> services)
    {
        var collection = new ServiceCollection().AddSingleton(_events);

        services(collection);

        return new PluginManager(
            collection.BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );
    }

    private static void Loaded(PluginManager manager)
    {
        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, status.Error);
    }

    private static async Task WaitForAsync(Func<bool> done)
    {
        for (var i = 0; i < 200 && !done(); i++)
            await Task.Delay(25, Ct);

        done().Should().BeTrue();
    }

    private sealed class Lifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _started = new();

        public CancellationToken ApplicationStarted => _started.Token;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void Start() => _started.Cancel();

        public void StopApplication() { }
    }

    // A plugin whose ready hook runs until the plugin stops, and says what happened in order.
    private const string WATCH = """
        using System.Collections.Concurrent;
        using System.Threading;
        using System.Threading.Tasks;
        using Turbo.Contracts.Plugins;

        public sealed class WatchPlugin : TurboPlugin
        {
            private ConcurrentQueue<string> Events(IPluginContext ctx) =>
                ctx.Host<ConcurrentQueue<string>>();

            protected override Task StartingAsync(IPluginContext ctx, CancellationToken ct)
            {
                Events(ctx).Enqueue("starting");

                return Task.CompletedTask;
            }

            protected override async Task HotelReadyAsync(IPluginContext ctx, CancellationToken ct)
            {
                Events(ctx).Enqueue("ready");

                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (System.OperationCanceledException) when (ctx.Stopping.IsCancellationRequested)
                {
                    Events(ctx).Enqueue("cancelled");
                }
            }

            protected override Task StoppingAsync(IPluginContext ctx, CancellationToken ct)
            {
                Events(ctx).Enqueue("stopping");

                return Task.CompletedTask;
            }
        }
        """;

    // Its own service and its own hosted service, each taking the host's clock by constructor.
    private const string STAMP = """
        using System;
        using System.Collections.Concurrent;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Hosting;
        using Turbo.Contracts.Plugins;

        public sealed class Stamp(TimeProvider clock, IHostServices host)
        {
            public void Write() =>
                host.GetRequiredService<ConcurrentQueue<string>>().Enqueue(clock.GetUtcNow() > DateTimeOffset.MinValue ? "stamped" : "no clock");
        }

        public sealed class Ticker(TimeProvider clock, Stamp stamp) : IHostedService
        {
            public Task StartAsync(CancellationToken ct)
            {
                stamp.Write();

                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
        }

        public sealed class StampPlugin : TurboPlugin
        {
            protected override void Services(IServiceCollection services)
            {
                services.AddSingleton<Stamp>();
                services.AddHostedService<Ticker>();
            }

            protected override Task StartingAsync(IPluginContext ctx, CancellationToken ct)
            {
                ctx.Host<ConcurrentQueue<string>>().Enqueue("hosted");

                return Task.CompletedTask;
            }
        }
        """;
}
