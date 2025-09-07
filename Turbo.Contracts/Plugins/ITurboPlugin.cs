using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Plugins;

public interface ITurboPlugin : IAsyncDisposable
{
    string PluginId { get; }
    string PluginName { get; }
    string PluginAuthor { get; }
    Version PluginVersion { get; }

    void ProcessManifest(PluginManifest manifest);
    ValueTask OnEnableAsync(CancellationToken ct); // handlers live
    ValueTask OnDisableAsync(CancellationToken ct); // stop work, unsubscribe if needed
}
