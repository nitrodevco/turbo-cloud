using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;

namespace Turbo.Gamedata.ClientModules;

/// <summary>
/// Sends everyone online the client modules again whenever a plugin adds or takes one away. The
/// presence drops it for a session that did not accept <c>client.modules</c>, so nothing here asks.
/// </summary>
internal sealed class ClientModuleBroadcast(
    IClientModuleDirectory modules,
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    ILogger<ClientModuleBroadcast> logger
) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        modules.Changed += OnChanged;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        modules.Changed -= OnChanged;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Raised inside a plugin's load or unload, so the sends are not awaited there: each is its
    /// own grain call, logged if it fails.
    /// </summary>
    internal void OnChanged()
    {
        var message = new TurboClientModulesMessage { Modules = modules.Modules };

        foreach (var playerId in sessionGateway.GetOnlinePlayerIds())
            grainFactory
                .SendComposerToPlayerAsync(playerId, message, CancellationToken.None)
                .LogAndForget(logger, "send the client modules to player {PlayerId}", playerId);
    }
}
