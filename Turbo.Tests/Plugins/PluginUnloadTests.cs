using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Plugins;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// A plugin's assembly load context goes away when the plugin is unloaded or reloaded. Every
/// plugin logged "ALC for plugin ... did not unload within timeout", even one that holds nothing:
/// the wait kept the context itself (its parameter) and the caller kept the plugin (its envelope,
/// in the reload still the live one) while it waited, so the context could never be collected and
/// each unload or reload waited the full 5 seconds.
/// The unload waits on garbage collection, which the rest of the suite running beside it can
/// delay past that timeout, so these run on their own.
/// </summary>
[CollectionDefinition("Plugin unload", DisableParallelization = true)]
public class PluginUnloadCollection { }

[Collection("Plugin unload")]
public sealed class PluginUnloadTests : IDisposable
{
    private const string LEAK = "did not unload";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-unload-" + Guid.NewGuid().ToString("N")
    );

    // The errors' text only, as a server's log keeps them: one thrown in a plugin's code holds it.
    private readonly CapturingLogger<PluginManager> _log = new(keepExceptions: false);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_plugin_that_holds_nothing_unloads_and_reloads_without_a_leak()
    {
        PluginBuilder.Write(_folder, "sample", SOURCE);

        var manager = new PluginManager(
            new ServiceCollection().BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            _log
        );

        await manager.LoadAllAsync(ct: Ct);
        manager.Snapshot().Single().State.Should().Be(PluginStateType.Loaded);

        await manager.ReloadAsync("sample", Ct);
        manager.Snapshot().Single().State.Should().Be(PluginStateType.Loaded);

        await manager.UnloadAllAsync(Ct);

        _log.AtLeast(LogLevel.Warning).Where(x => x.Message.Contains(LEAK)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_plugin_with_a_database_context_unloads_and_reloads_without_a_leak()
    {
        // The host's database settings, as a plugin reaches them; the version is given, so
        // nothing connects.
        var host = new ServiceCollection()
            .AddSingleton(
                Options.Create(
                    new Turbo.Database.Configuration.DatabaseConfig
                    {
                        ConnectionString = "server=127.0.0.1;database=none",
                        ServerVersion = "8.0.0-mysql",
                    }
                )
            )
            .BuildServiceProvider();

        // The plugin is compiled against the assemblies loaded: EF Core's too, run alone.
        _ = typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly;
        PluginBuilder.Write(_folder, "stored", STORED);

        var manager = new PluginManager(
            host,
            new AssemblyProcessor([]),
            // No time at all: only the collections it is given decide (merge gates 10-24 reported
            // it held after 5 seconds of collections slowed by the machine's load).
            Options.Create(
                new PluginConfig
                {
                    PluginFolderPath = _folder,
                    UnloadWaitMs = 0,
                    UnloadMinCollections = 50,
                }
            ),
            _log
        );

        await manager.LoadAllAsync(ct: Ct);
        manager
            .Snapshot()
            .Single()
            .State.Should()
            .Be(
                PluginStateType.Loaded,
                string.Join(
                    " | ",
                    _log.AtLeast(LogLevel.Warning).Select(x => x.Exception?.ToString())
                )
            );

        await manager.ReloadAsync("stored", Ct);
        manager.Snapshot().Single().State.Should().Be(PluginStateType.Loaded);

        await manager.UnloadAllAsync(Ct);

        _log.AtLeast(LogLevel.Warning).Where(x => x.Message.Contains(LEAK)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_plugin_that_fails_to_start_stops_what_it_started_and_unloads()
    {
        var events = new List<string>();
        var host = new ServiceCollection().AddSingleton(events).BuildServiceProvider();

        PluginBuilder.Write(_folder, "starts", FAILS_TO_START);

        var manager = new PluginManager(
            host,
            new AssemblyProcessor([]),
            Options.Create(
                new PluginConfig
                {
                    PluginFolderPath = _folder,
                    UnloadWaitMs = 0,
                    UnloadMinCollections = 50,
                }
            ),
            _log
        );

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();

        status.State.Should().Be(PluginStateType.Failed);
        status.Error.Should().Contain("cannot start");

        // As the server stops: the wait for the failed load's context is done by then.
        await manager.UnloadAllAsync(Ct);

        // Its hosted service ran on, and its context was never unloaded: only a live plugin's was.
        events.Should().Equal("started", "stopped");
        Loaded("starts").Should().Be(0);
        _log.AtLeast(LogLevel.Warning).Where(x => x.Message.Contains(LEAK)).Should().BeEmpty();
    }

    /// <summary>The plugin contexts not asked to unload that hold an assembly of this name.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Loaded(string assembly) =>
        AssemblyLoadContext
            .All.OfType<ByteLoadingAlc>()
            .Count(x => x.Assemblies.Any(a => a.GetName().Name == assembly));

    [Fact]
    public async Task A_context_freed_a_step_per_collection_is_waited_for_however_long_they_take()
    {
        // Like the methods generated for a plugin's types: a finalizer per collection lets go.
        var alc = HeldByFinalizers(steps: 10);

        (await AssemblyMemoryLoader.WaitForUnloadAsync(alc, maxMs: 0, Ct, minCollections: 50))
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task A_context_something_holds_is_reported_however_many_collections_it_is_given()
    {
        var held = new object();
        var alc = new WeakReference(held);

        (await AssemblyMemoryLoader.WaitForUnloadAsync(alc, maxMs: 0, Ct, minCollections: 50))
            .Should()
            .BeFalse();

        GC.KeepAlive(held);
    }

    [Fact]
    public async Task Compiling_a_plugin_does_not_hold_a_plugin_context_another_test_is_unloading()
    {
        // 10-25 dumps of the suite: a plugin compiled in one test held every plugin context
        // loaded at that moment for as long as its references were read (the list of loaded
        // assemblies they were taken from), and under load that outlasted 50 collections.
        var (alc, references) = CompiledWhileAnotherIsLoaded();

        (await AssemblyMemoryLoader.WaitForUnloadAsync(alc, maxMs: 0, Ct, minCollections: 50))
            .Should()
            .BeTrue();

        GC.KeepAlive(references);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (
        WeakReference Alc,
        IEnumerable<Microsoft.CodeAnalysis.MetadataReference> References
    ) CompiledWhileAnotherIsLoaded()
    {
        var alc = new AssemblyLoadContext("other", isCollectible: true);

        alc.LoadFromStream(
            new MemoryStream(PluginBuilder.Compile("other", "public class Other { }"))
        );

        var references = PluginBuilder.References();

        alc.Unload();

        return (new WeakReference(alc), references);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference HeldByFinalizers(int steps)
    {
        var context = new object();

        _ = new FinalizerStep(steps, GCHandle.Alloc(context));

        return new WeakReference(context);
    }

    private sealed class FinalizerStep(int left, GCHandle held)
    {
        ~FinalizerStep()
        {
            if (left > 0)
                _ = new FinalizerStep(left - 1, held);
            else
                held.Free();
        }
    }

    /// <summary>A plugin whose hosted service starts and whose own start then throws.</summary>
    private const string FAILS_TO_START = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Hosting;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;

        public sealed class StartsPlugin : ITurboPlugin
        {
            public string Key => "starts";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) =>
                services.AddHostedService(sp => new Ticker(
                    sp.GetRequiredService<IHostServices>().GetRequiredService<List<string>>()));
            public Task StartAsync(IServiceProvider services, CancellationToken ct) =>
                throw new InvalidOperationException("The plugin cannot start.");
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        public sealed class Ticker(List<string> events) : IHostedService
        {
            public Task StartAsync(CancellationToken ct) { events.Add("started"); return Task.CompletedTask; }
            public Task StopAsync(CancellationToken ct) { events.Add("stopped"); return Task.CompletedTask; }
        }
        """;

    /// <summary>A plugin with tables of its own: its context's model is built as it starts.</summary>
    private const string STORED = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.EntityFrameworkCore;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;
        using Turbo.Database.Extensions;

        public sealed class StoredPlugin : ITurboPlugin
        {
            public string Key => "stored";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) =>
                services.AddPluginDatabaseContext<StoredDbContext, StoredDbModule>();
            public Task StartAsync(IServiceProvider services, CancellationToken ct)
            {
                using var scope = services.CreateScope();
                _ = scope.ServiceProvider.GetRequiredService<StoredDbContext>().Model;
                return Task.CompletedTask;
            }
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        public sealed class StoredRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        public sealed class StoredDbContext(DbContextOptions<StoredDbContext> options) : DbContext(options)
        {
            public DbSet<StoredRow> Rows => Set<StoredRow>();
        }

        public sealed class StoredDbModule : IPluginDbModule
        {
            public Task MigrateAsync(IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
            public Task UninstallAsync(IServiceProvider sp, CancellationToken ct) => Task.CompletedTask;
        }
        """;

    private const string SOURCE = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;

        public sealed class SamplePlugin : ITurboPlugin
        {
            public string Key => "sample";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }
            public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
        """;
}
