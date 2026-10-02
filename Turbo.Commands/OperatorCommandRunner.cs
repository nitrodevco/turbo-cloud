using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Events;
using Turbo.Primitives.Action;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Events;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Texts;

namespace Turbo.Commands;

/// <summary>
/// Runs an operator command outside any room's turn. It does what a room does for a room command,
/// in the same order, so a player cannot tell the two apart: a node, arguments that fit, a plugin
/// veto, the command, its reply, and the use written down. What differs is who is asked: the
/// permission grain, not a copy on an avatar, and a reply is a whisper through the room. The console
/// holds every node and sees its replies on the screen.
/// </summary>
public sealed class OperatorCommandRunner(
    ICommandRegistryProvider registryProvider,
    IHotelTextProvider textProvider,
    EventSystem eventSystem,
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    ICommandBatchExecutor batchExecutor,
    IPlayerNoticeService notices,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<CommandConfig> config,
    TimeProvider timeProvider,
    ILogger<IOperatorCommandRunner> logger
) : IOperatorCommandRunner, IDisposable
{
    /// <summary>The console's key in <see cref="_pending"/>; no player has id 0.</summary>
    private const int CONSOLE_KEY = 0;

    /// <summary>The line each executor was last asked to confirm, by player id.</summary>
    private readonly ConcurrentDictionary<int, PendingConfirmation> _pending = new();

    private readonly OperatorExecutionAdmission _admission = new();
    private readonly object _pendingSync = new();
    private ITimer? _cleanupTimer;
    private bool _disposed;

    public async Task<CommandOutcome> RunAsync(
        CommandDescriptor descriptor,
        IOperatorExecutor executor,
        string argumentText,
        bool checkNode,
        CancellationToken ct
    )
    {
        lock (_pendingSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _cleanupTimer ??= timeProvider.CreateTimer(
                _ => CleanupPending(),
                null,
                TimeSpan.FromSeconds(config.Value.ConfirmationCleanupSeconds),
                TimeSpan.FromSeconds(config.Value.ConfirmationCleanupSeconds)
            );
        }

        using var lease = _admission.TryEnter(
            KeyOf(executor),
            config.Value.MaxOutstandingExecutions,
            config.Value.MaxOutstandingExecutionsPerExecutor
        );
        if (lease is null)
        {
            await ReplyAsync(executor, descriptor, CommandReplyKeys.BUSY, [], null, ct);
            return CommandOutcome.Refused;
        }

        // A canceled queued invocation retains its FIFO barrier until its predecessor settles.
        await lease.Predecessor;
        using var deadline = new CancellationTokenSource(
            TimeSpan.FromSeconds(config.Value.ExecutionTimeoutSeconds),
            timeProvider
        );
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            ct,
            deadline.Token
        );
        return (
            await RunExecutionAsync(
                descriptor,
                executor,
                argumentText,
                checkNode,
                prepared: null,
                cancellation.Token
            )
        ).Outcome;
    }

    private void CleanupPending()
    {
        lock (_pendingSync)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            foreach (var pair in _pending)
                if (pair.Value.ExpiresAtUtc <= now)
                    _pending.TryRemove(pair.Key, out _);
        }
    }

    public void Dispose()
    {
        lock (_pendingSync)
        {
            _disposed = true;
            _cleanupTimer?.Dispose();
            _pending.Clear();
        }
    }

    private async Task<(CommandOutcome Outcome, CommandResult Result)> RunExecutionAsync(
        CommandDescriptor descriptor,
        IOperatorExecutor executor,
        string argumentText,
        bool checkNode,
        PendingConfirmation? prepared,
        CancellationToken ct,
        Guid? parentExecutionId = null
    )
    {
        using var measurement = CommandTelemetry.Start(
            descriptor.Name,
            executor.RoomId ?? RoomId.Invalid
        );

        OperatorCommandContext? context = null;
        context = new OperatorCommandContext(
            executor,
            argumentText,
            descriptor.SelectorNode,
            prepared is not null,
            config.Value.ConfirmAtPlayers,
            pendingCt => ConfirmAsync(executor, context!, pendingCt),
            grainFactory,
            sessionGateway,
            batchExecutor,
            notices,
            descriptor.Name,
            prepared?.Audiences
        )
        {
            ParentExecutionId = parentExecutionId,
            ConfirmationId = prepared?.ExecutionId,
        };

        try
        {
            var (outcome, result) = await RunCoreAsync(
                descriptor,
                executor,
                context,
                argumentText,
                checkNode,
                prepared,
                ct
            );

            measurement.Complete(outcome);
            await FinalizeAsync(descriptor, executor, context, argumentText, outcome, result);

            return (outcome, result);
        }
        catch (OperationCanceledException)
        {
            measurement.Complete(CommandOutcome.Canceled);
            await FinalizeAsync(
                descriptor,
                executor,
                context,
                argumentText,
                CommandOutcome.Canceled,
                default
            );

            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Operator command {Command} failed for {Executor}",
                descriptor.Name,
                executor.Name
            );
            measurement.Complete(CommandOutcome.Error);
            await ReplyAsync(
                executor,
                descriptor,
                CommandReplyKeys.FAILED,
                [],
                null,
                CancellationToken.None
            );
            await FinalizeAsync(
                descriptor,
                executor,
                context,
                argumentText,
                CommandOutcome.Error,
                default
            );
            return (CommandOutcome.Error, default);
        }
    }

    public async Task<bool> TryRunLineAsync(
        string line,
        IOperatorExecutor executor,
        CancellationToken ct
    )
    {
        var text = line.AsSpan().Trim();

        if (text.Length > 0 && text[0] == ':')
            text = text[1..];

        var end = 0;

        while (end < text.Length && !char.IsWhiteSpace(text[end]))
            end++;

        if (end == 0 || !registryProvider.Current.TryFind(text[..end], out var descriptor))
            return false;

        if (!descriptor.IsOperator)
        {
            await executor.ReplyAsync(
                Text(CommandReplyKeys.NEEDS_ROOM, descriptor, null, []) ?? string.Empty,
                ct
            );

            return true;
        }

        await RunAsync(descriptor, executor, text[end..].ToString(), checkNode: true, ct);

        return true;
    }

    private async Task<(CommandOutcome, CommandResult)> RunCoreAsync(
        CommandDescriptor descriptor,
        IOperatorExecutor executor,
        OperatorCommandContext context,
        string argumentText,
        bool checkNode,
        PendingConfirmation? prepared,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        if (
            !registryProvider.Current.TryFind(descriptor.Name, out var current)
            || !ReferenceEquals(current, descriptor)
        )
        {
            await ReplyAsync(executor, descriptor, CommandReplyKeys.FAILED, [], null, ct);
            return (CommandOutcome.Refused, default);
        }

        if (!await HoldsANodeAsync(executor, descriptor, ct))
        {
            await ReplyAsync(executor, descriptor, CommandReplyKeys.NO_PERMISSION, [], null, ct);

            return (CommandOutcome.Refused, default);
        }

        var branchPermissions = (
            await Task.WhenAll(
                descriptor
                    .Binder.Syntax.Select(x => x.Permission)
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal)
                    .Select(async node => new KeyValuePair<string, bool>(
                        node,
                        await executor.HasAsync(node, ct)
                    ))
            )
        ).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var bound = prepared is null
            ? descriptor.Binder.Bind(
                argumentText,
                null,
                node => branchPermissions.GetValueOrDefault(node)
            )
            : CommandBindResult.Success(prepared.Arguments) with
            {
                RequiredPermission = prepared.RequiredPermission,
            };

        context.RequiredPermission = bound.RequiredPermission;
        if (bound.RequiredPermission is { } required && !await executor.HasAsync(required, ct))
        {
            await ReplyAsync(executor, descriptor, CommandReplyKeys.NO_PERMISSION, [], null, ct);
            return (CommandOutcome.Refused, default);
        }

        if (!bound.Succeeded)
        {
            await ReplyAsync(executor, descriptor, bound.ErrorKey!, bound.Parameters, null, ct);

            return (CommandOutcome.BindFailed, default);
        }

        // A plugin vetoes on its own state, so it is told only about a player in a room: the
        // console has no player state to veto on.
        if (executor.PlayerId is { } playerId && executor.RoomId is { } roomId)
        {
            var executing = new CommandExecutingEvent
            {
                RoomId = roomId,
                CausedBy = ActionContext.CreateForPlayer(playerId, roomId),
                PlayerId = playerId,
                Descriptor = descriptor,
                Arguments = bound.Arguments!,
            };

            await eventSystem.PublishAsync(executing, ct);

            if (executing.IsCancelled)
                return (CommandOutcome.Vetoed, default);
        }

        CommandResult result;

        try
        {
            result = await ((IOperatorCommand)descriptor.Command).ExecuteAsync(
                context,
                bound.Arguments!,
                ct
            );
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Operator command {Command} failed for {Executor} in room {RoomId}",
                descriptor.Name,
                executor.PlayerId?.ToString() ?? executor.Name,
                executor.RoomId
            );

            await ReplyAsync(executor, descriptor, CommandReplyKeys.FAILED, [], null, ct);

            return (CommandOutcome.Error, default);
        }

        if (result.NeedsConfirmation && result.Status is { } question)
        {
            if (
                !await AskToConfirmAsync(
                    descriptor,
                    executor,
                    context,
                    argumentText,
                    bound.Arguments!,
                    question,
                    result,
                    ct
                )
            )
                return (CommandOutcome.Refused, default);

            return (CommandOutcome.AwaitingConfirmation, result);
        }

        if (result.Status is { } status)
            await ReplyAsync(
                executor,
                descriptor,
                CommandReplyKeys.IsShared(status)
                    ? status
                    : CommandReplyKeys.ForCommand(descriptor.Name, status),
                result.Parameters,
                CommandReplyKeys.IsShared(status) ? null : status,
                ct
            );

        if (context.NoticeFailures > 0)
            await ReplyAsync(
                executor,
                descriptor,
                CommandReplyKeys.NOTICE_FAILED,
                [context.NoticeFailures.ToString(CultureInfo.InvariantCulture)],
                null,
                ct
            );
        if (context.OfflineNotices > 0)
            await ReplyAsync(
                executor,
                descriptor,
                CommandReplyKeys.NOTICE_OFFLINE,
                [context.OfflineNotices.ToString(CultureInfo.InvariantCulture)],
                null,
                ct
            );

        return (result.Outcome, result);
    }

    /// <summary>
    /// Keeps the line for <c>:confirm</c>, replacing any the executor left unconfirmed, and tells
    /// them what it would do and how to go ahead.
    /// </summary>
    private async Task<bool> AskToConfirmAsync(
        CommandDescriptor descriptor,
        IOperatorExecutor executor,
        OperatorCommandContext context,
        string argumentText,
        object arguments,
        string question,
        CommandResult result,
        CancellationToken ct
    )
    {
        var seconds = config.Value.ConfirmationSeconds;

        var accepted = false;
        lock (_pendingSync)
        {
            CleanupPending();
            if (
                !_disposed
                && (
                    _pending.ContainsKey(KeyOf(executor))
                    || _pending.Count < config.Value.MaxPendingConfirmations
                )
            )
            {
                context.ConfirmationId = context.ExecutionId;
                _pending[KeyOf(executor)] = new PendingConfirmation(
                    descriptor,
                    executor.RoomId,
                    argumentText,
                    arguments,
                    context.RequiredPermission,
                    context.ExecutionId,
                    new Dictionary<string, IReadOnlyList<PlayerId>>(
                        context.Audiences,
                        StringComparer.OrdinalIgnoreCase
                    ),
                    timeProvider.GetUtcNow().UtcDateTime.AddSeconds(seconds)
                );
                accepted = true;
            }
        }
        if (!accepted)
        {
            await ReplyAsync(executor, descriptor, CommandReplyKeys.BUSY, [], null, ct);
            return false;
        }

        var shared = CommandReplyKeys.IsShared(question);
        var what =
            Text(
                shared ? question : CommandReplyKeys.ForCommand(descriptor.Name, question),
                descriptor,
                shared ? null : question,
                result.Parameters
            ) ?? string.Empty;

        await ReplyAsync(
            executor,
            descriptor,
            CommandReplyKeys.CONFIRM_PROMPT,
            [what, seconds.ToString(CultureInfo.InvariantCulture)],
            null,
            ct
        );
        return true;
    }

    /// <summary>
    /// Runs the prepared command with the confirming executor's current permissions, checking
    /// that its room and command registration still match what was approved.
    /// </summary>
    private async Task<CommandResult> ConfirmAsync(
        IOperatorExecutor executor,
        OperatorCommandContext caller,
        CancellationToken ct
    )
    {
        if (
            !_pending.TryRemove(KeyOf(executor), out var pending)
            || pending.ExpiresAtUtc <= timeProvider.GetUtcNow().UtcDateTime
            || !registryProvider.Current.TryFind(pending.Descriptor.Name, out var descriptor)
            || !ReferenceEquals(descriptor, pending.Descriptor)
        )
            return CommandResult.Fail(CommandReplyKeys.NOTHING_TO_CONFIRM);

        if (pending.RoomId != executor.RoomId)
            return CommandResult.Fail(CommandReplyKeys.CONFIRM_ELSEWHERE);

        caller.ConfirmationId = pending.ExecutionId;

        var (outcome, result) = await RunExecutionAsync(
            descriptor,
            executor,
            pending.ArgumentText,
            checkNode: true,
            prepared: pending,
            ct,
            parentExecutionId: caller.ExecutionId
        );

        return result with
        {
            Status = null,
            Parameters = [],
            IsFailure = outcome != CommandOutcome.Completed,
        };
    }

    private static int KeyOf(IOperatorExecutor executor) => executor.PlayerId?.Value ?? CONSOLE_KEY;

    /// <summary>Audits the terminal result before notifying observers. Neither step may rewrite
    /// that result or prevent the other step; each gets a bounded token independent of the caller.</summary>
    private async Task FinalizeAsync(
        CommandDescriptor descriptor,
        IOperatorExecutor executor,
        OperatorCommandContext context,
        string argumentText,
        CommandOutcome outcome,
        CommandResult result
    )
    {
        await TryFinalizeAsync(
            "audit",
            token =>
                LogUseAsync(descriptor, executor, context, argumentText, outcome, result, token)
        );
        await TryFinalizeAsync(
            "completion hooks",
            token => PublishExecutedAsync(descriptor, executor, outcome, result, token)
        );

        async Task TryFinalizeAsync(string step, Func<CancellationToken, Task> action)
        {
            try
            {
                using var timeout = new CancellationTokenSource(
                    TimeSpan.FromSeconds(config.Value.FinalizationTimeoutSeconds),
                    timeProvider
                );
                await action(timeout.Token).WaitAsync(timeout.Token);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Command {Command} {Step} failed for {Executor}; outcome remains {Outcome}",
                    descriptor.Name,
                    step,
                    executor.PlayerId?.ToString() ?? executor.Name,
                    outcome
                );
            }
        }
    }

    private static async Task<bool> HoldsANodeAsync(
        IOperatorExecutor executor,
        CommandDescriptor descriptor,
        CancellationToken ct
    )
    {
        foreach (var node in descriptor.Nodes)
            if (await executor.HasAsync(node, ct))
                return true;

        return false;
    }

    private async Task PublishExecutedAsync(
        CommandDescriptor descriptor,
        IOperatorExecutor executor,
        CommandOutcome outcome,
        CommandResult result,
        CancellationToken ct
    )
    {
        if (executor.PlayerId is not { } playerId || executor.RoomId is not { } roomId)
            return;

        await eventSystem.PublishAsync(
            new CommandExecutedEvent
            {
                RoomId = roomId,
                CausedBy = ActionContext.CreateForPlayer(playerId, roomId),
                PlayerId = playerId,
                Descriptor = descriptor,
                Outcome = outcome,
                Result = result,
            },
            ct
        );
    }

    /// <summary>
    /// Writes the use down when the executor holds <c>command.log</c>, or when the line used a
    /// selector, which is always logged: aiming a command at everyone is the use an operator most
    /// needs a record of. A failed write is logged and nothing more; the command already ran.
    /// </summary>
    private async Task LogUseAsync(
        CommandDescriptor descriptor,
        IOperatorExecutor executor,
        OperatorCommandContext context,
        string argumentText,
        CommandOutcome outcome,
        CommandResult result,
        CancellationToken ct
    )
    {
        try
        {
            if (!context.UsedSelector && !await executor.HasAsync(PermissionNodes.Command.LOG, ct))
                return;

            await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

            dbCtx.CommandLogs.Add(
                new CommandLogEntity
                {
                    RoomEntityId = executor.RoomId?.Value ?? 0,
                    PlayerEntityId = executor.PlayerId?.Value ?? 0,
                    Command = Truncate(descriptor.Name, CommandLogEntity.COMMAND_MAX_LENGTH),
                    Arguments = Truncate(
                        descriptor.Binder.AuditText(argumentText).Trim(),
                        CommandLogEntity.ARGUMENTS_MAX_LENGTH
                    ),
                    Outcome = Truncate(
                        CommandTelemetry.Name(outcome),
                        CommandLogEntity.OUTCOME_MAX_LENGTH
                    ),
                    ExecutionId = context.ExecutionId,
                    ParentExecutionId = context.ParentExecutionId,
                    ConfirmationId = context.ConfirmationId,
                    Source = executor.PlayerId is null ? "console" : "player",
                    ResolvedAudienceJson = JsonSerializer.Serialize(
                        context.Audiences.ToDictionary(
                            x => x.Key,
                            x => x.Value.Select(id => id.Value).ToArray()
                        )
                    ),
                    BatchTargetResultsJson = result.Batch is { } batch
                        ? JsonSerializer.Serialize(
                            batch.Targets.Select(target => new
                            {
                                PlayerId = target.PlayerId.Value,
                                target.Requested,
                                target.Succeeded,
                                target.Failed,
                                target.Indeterminate,
                                target.Unattempted,
                            })
                        )
                        : null,
                }
            );

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to write the command log entry for {Command} run by {Executor}",
                descriptor.Name,
                executor.PlayerId?.ToString() ?? executor.Name
            );
        }
    }

    private async Task ReplyAsync(
        IOperatorExecutor executor,
        CommandDescriptor descriptor,
        string key,
        string[] parameters,
        string? status,
        CancellationToken ct
    )
    {
        try
        {
            if (Text(key, descriptor, status, parameters) is not { } text)
                return;

            // Feedback describes the settled operation, even if its request deadline elapsed.
            using var deadline = new CancellationTokenSource(
                TimeSpan.FromSeconds(config.Value.FinalizationTimeoutSeconds),
                timeProvider
            );
            await executor.ReplyAsync(text, deadline.Token).WaitAsync(deadline.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to answer {Executor} for command {Command}",
                executor.PlayerId?.ToString() ?? executor.Name,
                descriptor.Name
            );
        }
    }

    /// <summary>
    /// The hotel's text for the key, else core's default for a shared key, else the command's own
    /// for its status; null when there is none, which a command wants when its status is only for
    /// the caller to read.
    /// </summary>
    private string? Text(
        string key,
        CommandDescriptor descriptor,
        string? status,
        string[] parameters
    )
    {
        if (
            !textProvider.TryGetText(key, out var text)
            && !CommandReplyKeys.Defaults.TryGetValue(key, out text)
            && !(status is not null && descriptor.Texts.TryGetValue(status, out text))
        )
            return null;

        for (var i = 0; i < parameters.Length; i++)
            text = text.Replace($"%{i}%", parameters[i], StringComparison.Ordinal);

        return text;
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];
}
