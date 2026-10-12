using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Operations.Commands;

/// <summary>
/// A moderation command's record of what it did, by whoever ran it and from where: the game,
/// the panel (which runs these commands) or the console.
/// </summary>
internal static class CommandModeration
{
    public static Task RecordAsync(
        this IModerationLog log,
        IOperatorCommandContext ctx,
        ModerationActionType action,
        PlayerId target,
        CancellationToken ct,
        string? reason = null,
        DateTime? expiresAt = null,
        string? detail = null
    ) =>
        log.RecordAsync(
            new ModerationRecord
            {
                Action = action,
                Source = ModerationRecord.SourceOf(ctx.Executor),
                ActorId = ctx.Executor.PlayerId,
                TargetPlayerId = target,
                RoomId = ctx.Executor.RoomId,
                Reason = reason,
                ExpiresAt = expiresAt,
                Detail = detail,
            },
            ct
        );

    /// <summary>The reason typed after a command's other arguments, or null when none was.</summary>
    public static string? ReasonOf(RestOfLine? line) =>
        line?.Text.Trim() is { Length: > 0 } given ? given : null;
}
