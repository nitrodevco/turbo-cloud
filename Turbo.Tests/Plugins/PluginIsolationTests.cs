using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Turbo.Commands;
using Turbo.Contracts.Plugins;
using Turbo.Players.Permissions;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Plugins;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// One plugin that fails to load does not take the server or the other plugins with it, and leaves
/// nothing registered behind. Built with the host's real permission node and command processors,
/// in the order the modules add them.
/// </summary>
public sealed class PluginIsolationTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-isolation-" + Guid.NewGuid().ToString("N")
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
    public async Task A_plugin_whose_command_cannot_be_built_fails_alone_and_the_server_starts()
    {
        PluginBuilder.Write(_folder, "broken", Plugin("broken", NEEDS_MISSING));
        PluginBuilder.Write(_folder, "good", Plugin("good", NODES));

        var (manager, host) = Create();
        var bootstrapper = new PluginBootstrapper(
            [],
            manager,
            host.GetRequiredService<AssemblyProcessor>(),
            host
        );

        // The hosted service the server starts with: it threw, so "Hosting failed to start".
        await bootstrapper.StartAsync(Ct);

        var plugins = manager.Snapshot().ToDictionary(x => x.Key);

        plugins["good"].State.Should().Be(PluginStateType.Loaded);
        plugins["broken"].State.Should().Be(PluginStateType.Failed);
        plugins["broken"].Error.Should().Contain("IMissing");

        var nodes = host.GetRequiredService<IPermissionRegistryProvider>().Current;

        nodes.IsRegistered("good.use").Should().BeTrue();
        nodes.IsRegistered("broken.use").Should().BeFalse();
    }

    [Fact]
    public async Task A_plugin_that_failed_part_way_loads_once_fixed_without_a_duplicate_node()
    {
        PluginBuilder.Write(_folder, "half", Plugin("half", NEEDS_MISSING));

        var (manager, host) = Create();

        // Its permission nodes registered, then its command could not be built.
        await manager
            .Invoking(m => m.ReloadAsync("half", Ct))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*IMissing*");

        host.GetRequiredService<IPermissionRegistryProvider>()
            .Current.IsRegistered("half.use")
            .Should()
            .BeFalse();

        PluginBuilder.Write(_folder, "half", Plugin("half", NODES));

        // It failed "Permission node 'half.use' is registered twice" until a restart.
        await manager.ReloadAsync("half", Ct);

        manager.Snapshot().Single().State.Should().Be(PluginStateType.Loaded);
        host.GetRequiredService<IPermissionRegistryProvider>()
            .Current.IsRegistered("half.use")
            .Should()
            .BeTrue();
    }

    private (PluginManager Manager, ServiceProvider Host) Create(
        Action<IServiceCollection>? configure = null
    )
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IPermissionRegistryProvider, PermissionRegistryProvider>();
        services.AddSingleton<IAssemblyFeatureProcessor, PermissionNodeFeatureProcessor>();
        new CommandModule().ConfigureServices(
            services,
            Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings())
        );
        services.AddSingleton<AssemblyProcessor>();
        configure?.Invoke(services);

        var host = services.BuildServiceProvider();

        return (
            new PluginManager(
                host,
                host.GetRequiredService<AssemblyProcessor>(),
                Options.Create(new PluginConfig { PluginFolderPath = _folder }),
                _log
            ),
            host
        );
    }

    /// <summary>A plugin with a permission node <c>&lt;key&gt;.use</c> and the classes given.</summary>
    private static string Plugin(string key, string classes) =>
        $$"""
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using Turbo.Contracts.Plugins;
            using Turbo.Contracts.Plugins.Exports;
            using Turbo.Primitives.Commands;
            using Turbo.Primitives.Players.Permissions;

            public sealed class ThePlugin : ITurboPlugin
            {
                public string Key => "{{key}}";
                public string Version => "1.0.0";
                public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }
                public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
                public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }

            public sealed class TheNodes : IPermissionNodeSource
            {
                public string? Prefix => "{{key}}";
                public IEnumerable<PermissionNodeDefinition> Nodes => [new("{{key}}.use", "Use it")];
                public IEnumerable<PermissionMetaDefinition> MetaKeys => [];
            }

            {{classes}}
            """;

    private const string NODES = "";

    /// <summary>A command that takes a service nobody registered.</summary>
    private const string NEEDS_MISSING = """
        public interface IMissing { }

        [Command("needsmissing")]
        public sealed class NeedsMissingCommand(IMissing missing) : ICommand<NoArguments>
        {
            public ValueTask<CommandResult> ExecuteAsync(ICommandContext ctx, NoArguments arguments, CancellationToken ct) =>
                ValueTask.FromResult(CommandResult.Ok);
        }
        """;
}
