using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Turbo.Primitives.Orleans;

public static class TaskExtensions
{
    /// <summary>
    /// Fire-and-forget that still surfaces failures: a faulted task is logged instead of vanishing
    /// the way <c>.Ignore()</c> would let it.
    /// </summary>
    public static void LogAndForget(this Task task, ILogger logger, string operation)
    {
        _ = task.ContinueWith(
            t =>
                logger.LogError(
                    t.Exception?.GetBaseException(),
                    "Background operation failed: {Operation}",
                    operation
                ),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }
}
