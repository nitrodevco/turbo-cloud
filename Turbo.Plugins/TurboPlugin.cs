using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins;

public abstract class TurboPlugin : ITurboPlugin
{
    public virtual string PluginId { get; private set; } = string.Empty;
    public virtual string PluginName { get; private set; } = string.Empty;
    public virtual string PluginAuthor { get; private set; } = string.Empty;
    public virtual Version PluginVersion { get; private set; } = Version.Parse("1.0.0");

    public abstract ValueTask DisposeAsync();

    public void ProcessManifest(PluginManifest manifest)
    {
        PluginId = manifest.Id;
        PluginName = manifest.Name;
        PluginAuthor = manifest.Author;
        PluginVersion = Version.Parse(manifest.Version);
    }

    public virtual ValueTask OnEnableAsync(CancellationToken ct)
    {
        // handlers live
        return ValueTask.CompletedTask;
    }

    public virtual ValueTask OnDisableAsync(CancellationToken ct)
    {
        // stop work, unsubscribe if needed
        return ValueTask.CompletedTask;
    }
}
