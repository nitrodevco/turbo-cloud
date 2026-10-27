using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Contracts.Plugins.Exports;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Plugins.Messages;

namespace Hello;

/// <summary>
/// The smallest plugin with a client module: it declares <c>client/src/index.ts</c>, which the
/// server builds and a client that speaks <c>client.modules</c> loads and runs, and its icon
/// <c>wwwroot/hello.png</c>. Its texts and its bar's
/// colour and width come with it in <c>gamedata/</c>, given to the hotel as it loads. Its window's
/// button sends <see cref="Wave"/>, a named message, and the plugin answers with <see cref="Waved"/>
/// (<see cref="WaveHandler"/>). See <c>docs/client-modules.md</c> and <c>docs/plugin-messages.md</c>.
/// </summary>
public sealed class HelloPlugin : ITurboPlugin
{
    private IDisposable? _module;

    public string Key => "hello";

    public string Version => "1.0.0";

    public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }

    public Task StartAsync(IServiceProvider services, CancellationToken ct)
    {
        _module = services
            .GetRequiredService<IHostServices>()
            .GetRequiredService<IClientModuleRegistry>()
            .Register(Key, "client/src/index.ts");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        // The server takes it away on unload anyway; a plugin that can switch its module off at
        // run time disposes the handle itself.
        _module?.Dispose();
        _module = null;

        return Task.CompletedTask;
    }

    public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>What the module's window says about its clicks: a record inside a message.</summary>
public sealed record WaveStats(int Clicks, long At, ImmutableArray<int> Recent);

/// <summary>The module waves: client to server.</summary>
[PluginMessage("hello.wave")]
public sealed record Wave(string Greeting, WaveStats Stats) : IPluginMessage;

/// <summary>The plugin answers: server to client.</summary>
[PluginMessage("hello.waved")]
public sealed record Waved(string Text, WaveStats Stats, bool Even) : IPluginMessage;

/// <summary>Answers a wave on the session it came from; found and gated as a packet handler is.</summary>
public sealed class WaveHandler : IPluginMessageHandler<Wave>
{
    public async ValueTask HandleAsync(
        Wave message,
        PluginMessageContext ctx,
        CancellationToken ct
    ) =>
        await ctx.ReplyAsync(
            new Waved(
                $"{message.Greeting} back, player {ctx.PlayerId.Value}: {message.Stats.Clicks} clicks",
                message.Stats with
                {
                    Recent = [.. message.Stats.Recent.Reverse()],
                },
                message.Stats.Clicks % 2 == 0
            ),
            ct
        );
}
