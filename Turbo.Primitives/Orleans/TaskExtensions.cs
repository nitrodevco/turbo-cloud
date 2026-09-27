using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Turbo.Primitives.Orleans;

public static class TaskExtensions
{
    private const TaskContinuationOptions ON_FAULT =
        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously;

    /// <summary>
    /// Fire-and-forget that still surfaces failures: a faulted task is logged instead of vanishing
    /// the way <c>.Ignore()</c> would let it.
    /// </summary>
    /// <remarks>
    /// Prefer the template overloads for anything that names an id: they log the ids as
    /// properties, and they format nothing unless the task fails, where an interpolated
    /// <paramref name="operation"/> is built on every call.
    /// </remarks>
    public static void LogAndForget(this Task task, ILogger logger, string operation)
    {
        if (task.IsCompletedSuccessfully)
            return;

        _ = task.ContinueWith(
            static (t, state) =>
            {
                var (logger, operation) = ((ILogger, string))state!;

                logger.LogError(
                    t.Exception?.GetBaseException(),
                    "Background operation failed: {Operation}",
                    operation
                );
            },
            (logger, operation),
            CancellationToken.None,
            ON_FAULT,
            TaskScheduler.Default
        );
    }

    /// <summary>
    /// <see cref="LogAndForget(Task, ILogger, string)"/> with the operation as a message
    /// template, logged as "Failed to " + <paramref name="operationTemplate"/>:
    /// <c>.LogAndForget(_logger, "set player {PlayerId} online", playerId)</c>. The arguments are
    /// only formatted if the task fails, and an already completed task costs nothing.
    /// </summary>
    public static void LogAndForget<T1>(
        this Task task,
        ILogger logger,
        string operationTemplate,
        T1 arg1
    )
    {
        if (task.IsCompletedSuccessfully)
            return;

        _ = task.ContinueWith(
            static (t, state) =>
            {
                var (logger, template, arg1) = ((ILogger, string, T1))state!;

                logger.LogError(t.Exception?.GetBaseException(), "Failed to " + template, arg1);
            },
            (logger, operationTemplate, arg1),
            CancellationToken.None,
            ON_FAULT,
            TaskScheduler.Default
        );
    }

    /// <inheritdoc cref="LogAndForget{T1}(Task, ILogger, string, T1)"/>
    public static void LogAndForget<T1, T2>(
        this Task task,
        ILogger logger,
        string operationTemplate,
        T1 arg1,
        T2 arg2
    )
    {
        if (task.IsCompletedSuccessfully)
            return;

        _ = task.ContinueWith(
            static (t, state) =>
            {
                var (logger, template, arg1, arg2) = ((ILogger, string, T1, T2))state!;

                logger.LogError(
                    t.Exception?.GetBaseException(),
                    "Failed to " + template,
                    arg1,
                    arg2
                );
            },
            (logger, operationTemplate, arg1, arg2),
            CancellationToken.None,
            ON_FAULT,
            TaskScheduler.Default
        );
    }

    /// <inheritdoc cref="LogAndForget{T1}(Task, ILogger, string, T1)"/>
    public static void LogAndForget<T1, T2, T3>(
        this Task task,
        ILogger logger,
        string operationTemplate,
        T1 arg1,
        T2 arg2,
        T3 arg3
    )
    {
        if (task.IsCompletedSuccessfully)
            return;

        _ = task.ContinueWith(
            static (t, state) =>
            {
                var (logger, template, arg1, arg2, arg3) = ((ILogger, string, T1, T2, T3))state!;

                logger.LogError(
                    t.Exception?.GetBaseException(),
                    "Failed to " + template,
                    arg1,
                    arg2,
                    arg3
                );
            },
            (logger, operationTemplate, arg1, arg2, arg3),
            CancellationToken.None,
            ON_FAULT,
            TaskScheduler.Default
        );
    }

    /// <summary>
    /// <see cref="LogAndForget{T1}(Task, ILogger, string, T1)"/> for more than three
    /// arguments.
    /// </summary>
    public static void LogAndForget(
        this Task task,
        ILogger logger,
        string operationTemplate,
        params object?[] args
    )
    {
        if (task.IsCompletedSuccessfully)
            return;

        _ = task.ContinueWith(
            static (t, state) =>
            {
                var (logger, template, args) = ((ILogger, string, object?[]))state!;

                logger.LogError(t.Exception?.GetBaseException(), "Failed to " + template, args);
            },
            (logger, operationTemplate, args),
            CancellationToken.None,
            ON_FAULT,
            TaskScheduler.Default
        );
    }
}
