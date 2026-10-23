using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>What a test plugin keeps hold of, so its old version cannot be collected.</summary>
public static class ReloadRetainer
{
    public static readonly List<object> Held = [];
}

/// <summary>
/// A reload starts the plugin's new version and tells clients its new module at once, so the new
/// version's handlers (and every other assembly feature) must be registered then too - not after
/// the old version's assembly context has been collected, which a plugin that leaves a reference
/// behind holds up for the whole unload timeout (packets from its window went unanswered).
/// </summary>
public sealed class PluginReloadOrderTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-reload-order-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        ReloadRetainer.Held.Clear();

        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_reloaded_plugins_features_are_registered_before_its_old_version_is_waited_for()
    {
        var logger = new CapturingLogger<PluginManager>();
        var processor = new RecordingProcessor(logger);
        var manager = new PluginManager(
            new ServiceCollection().BuildServiceProvider(),
            new AssemblyProcessor([processor]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            logger
        );

        PluginBuilder.Write(_folder, "retained", Source);
        await manager.LoadAllAsync(ct: Ct);

        // A new build of the plugin, as a dev loop writes it.
        PluginBuilder.Write(_folder, "retained", Source + "\n// v2");
        await manager.LoadAllAsync(ct: Ct);

        processor.Processed.Should().HaveCount(2);
        logger
            .AtLeast(LogLevel.Warning)
            .Should()
            .Contain(e => e.Message.Contains("did not unload"), "the old version is held");
        processor
            .Processed[1]
            .UnloadWarningsBefore.Should()
            .Be(0, "the new version's features were registered before the old one was waited for");

        await manager.UnloadAllAsync(Ct);
    }

    private const string Source = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;

        public sealed class RetainedPlugin : ITurboPlugin
        {
            public string Key => "retained";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }
            public Task StartAsync(IServiceProvider services, CancellationToken ct)
            {
                Turbo.Tests.Plugins.ReloadRetainer.Held.Add(this);
                return Task.CompletedTask;
            }
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
        """;

    private sealed class RecordingProcessor(CapturingLogger<PluginManager> logger)
        : IAssemblyFeatureProcessor
    {
        public List<(Assembly Assembly, int UnloadWarningsBefore)> Processed { get; } = [];

        public Task<IDisposable> ProcessAsync(
            Assembly assembly,
            IServiceProvider pluginServices,
            CancellationToken ct = default
        )
        {
            Processed.Add(
                (
                    assembly,
                    logger
                        .AtLeast(LogLevel.Warning)
                        .Count(e => e.Message.Contains("did not unload"))
                )
            );

            return Task.FromResult<IDisposable>(new Nothing());
        }

        private sealed class Nothing : IDisposable
        {
            public void Dispose() { }
        }
    }
}
