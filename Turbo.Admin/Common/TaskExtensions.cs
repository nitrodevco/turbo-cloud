using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Turbo.Admin.Common;

internal static class TaskExtensions
{
    public static void LogAndForget(this Task task, ILogger logger, string message) =>
        _ = task.ContinueWith(
            t => logger.LogError(t.Exception, "{Message}", message),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default
        );
}
