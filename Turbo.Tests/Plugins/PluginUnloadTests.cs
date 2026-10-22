using System.IO;
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
/// </summary>
public sealed class PluginUnloadTests : IDisposable
{
    private const string LEAK = "did not unload";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-unload-" + Guid.NewGuid().ToString("N")
    );

    private readonly CapturingLogger<PluginManager> _log = new();

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
