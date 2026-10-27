using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Ai;

/// <summary>
/// Runs started a minute, per plugin and per player (<c>Turbo:Ai:PluginRunsPerMinute</c>,
/// <c>PlayerRunsPerMinute</c>), over the last 60 seconds. A server counts them for the whole
/// cluster (<see cref="ClusterAiRateLimits"/>); a test kit on its own (<see cref="AiRateLimits"/>).
/// </summary>
public interface IAiRateLimits
{
    /// <summary>Counts a run for each key, when every key is under its limit (0 for none); false, counting nothing, when one is over.</summary>
    Task<bool> TryStartAsync(CancellationToken ct, params (string Key, int Limit)[] keys);
}
