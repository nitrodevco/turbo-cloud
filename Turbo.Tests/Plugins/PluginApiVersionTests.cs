using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Plugins;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The plugin API a server loads: a plugin folder built for the oldest one it still takes or its
/// own loads; one built for a newer API (or one older than the oldest) is refused at load as at
/// install, the message naming both versions.
/// </summary>
public sealed class PluginApiVersionTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-api-" + Guid.NewGuid().ToString("N")
    );

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Theory]
    [InlineData(PluginApi.OLDEST, true)]
    [InlineData(PluginApi.VERSION, true)]
    [InlineData(PluginApi.VERSION + 1, false)]
    [InlineData(PluginApi.OLDEST - 1, false)]
    public async Task A_plugin_folder_loads_only_for_an_api_the_server_takes(int api, bool loads)
    {
        PluginBuilder.Write(_folder, "sample", SOURCE, api);

        var manager = new PluginManager(
            new ServiceCollection().BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );

        await manager.LoadAllAsync(ct: TestContext.Current.CancellationToken);

        var status = manager.Snapshot().Single();

        if (loads)
        {
            status.State.Should().Be(PluginStateType.Loaded, status.Error);
            await manager.UnloadAllAsync(TestContext.Current.CancellationToken);

            return;
        }

        status.State.Should().Be(PluginStateType.Failed);
        status
            .Error.Should()
            .Contain($"built for plugin API {api} (turbo-cloud abc1234)")
            .And.Contain($"this server has plugin API {PluginApi.VERSION}");
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
