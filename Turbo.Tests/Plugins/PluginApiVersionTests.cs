using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Contracts.Plugins;
using Turbo.Operations.Movements;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Movements;
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

    // OLDEST is VERSION until a published plugin is broken by a change, so one row covers both.
    [Theory]
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

    [Fact]
    public async Task A_plugin_built_for_this_api_consumes_movements_through_the_host()
    {
        PluginBuilder.Write(_folder, "ledger", CONSUMER, PluginApi.VERSION);

        var registry = new AssetMovementConsumerRegistry(
            new Fakes().Create<IGrainFactory>(),
            NullLogger<AssetMovementConsumerRegistry>.Instance
        );
        var manager = new PluginManager(
            new ServiceCollection()
                .AddSingleton<IAssetMovementConsumerRegistry>(registry)
                .BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );

        await manager.LoadAllAsync(ct: TestContext.Current.CancellationToken);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, status.Error);
        registry.Current.Select(x => x.Consumer.Name).Should().Equal("ledger");

        await manager.UnloadAllAsync(TestContext.Current.CancellationToken);

        registry.Current.Should().BeEmpty("the plugin disposed its registration when it stopped");
    }

    private const string CONSUMER = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;
        using Turbo.Primitives.Movements;

        public sealed class LedgerPlugin : ITurboPlugin, IAssetMovementConsumer
        {
            private IDisposable _consumer;

            public string Key => "ledger";
            public string Version => "1.0.0";
            public string Name => "ledger";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }

            public Task StartAsync(IServiceProvider services, CancellationToken ct)
            {
                _consumer = services.GetRequiredService<IHostServices>()
                    .GetRequiredService<IAssetMovementConsumerRegistry>()
                    .Register(this);

                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken ct)
            {
                _consumer?.Dispose();

                return Task.CompletedTask;
            }

            public Task<long> GetCursorAsync(CancellationToken ct) => Task.FromResult(0L);
            public Task HandleAsync(AssetMovementBatch batch, CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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
