using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Plugins;

/// <summary>
/// Told as a plugin starts to stop, before its hosted services, its own <c>StopAsync</c> and its
/// services go: what the host runs on the plugin's behalf (its scheduled tasks) is stopped while
/// the plugin can still finish it. Registered in the host's services; one that throws is logged
/// and the unload goes on.
/// </summary>
public interface IPluginStopping
{
    Task StoppingAsync(string pluginKey, CancellationToken ct);
}
