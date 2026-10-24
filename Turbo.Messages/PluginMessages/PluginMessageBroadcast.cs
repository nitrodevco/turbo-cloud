using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Plugins.Messages;

namespace Turbo.Messages.PluginMessages;

/// <summary>
/// Sends everyone online the plugin messages again whenever a plugin adds or takes some away. The
/// presence drops it for a session that did not accept <c>client.messages</c>, so nothing here asks.
/// </summary>
internal sealed class PluginMessageBroadcast(
    IPluginMessageDirectory messages,
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    ILogger<PluginMessageBroadcast> logger
) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        messages.Changed += OnChanged;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        messages.Changed -= OnChanged;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Raised inside a plugin's load or unload, so the sends are not awaited there: each is its
    /// own grain call, logged if it fails.
    /// </summary>
    internal void OnChanged()
    {
        var message = new TurboPluginMessagesMessage { Messages = messages.Messages };

        foreach (var playerId in sessionGateway.GetOnlinePlayerIds())
            grainFactory
                .SendComposerToPlayerAsync(playerId, message, CancellationToken.None)
                .LogAndForget(logger, "send the plugin messages to player {PlayerId}", playerId);
    }
}
