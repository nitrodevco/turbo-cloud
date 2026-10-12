using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Moderation;
using Turbo.Primitives.Moderation;

namespace Turbo.Operations;

/// <summary>
/// Writes each moderation action as it is taken, one row at a time: they come a few a minute at
/// most, and staff look for one the moment it is done, so there is nothing to gain from holding
/// them back. A write that fails is logged with everything it would have recorded, so the
/// record is in the server log at least, and the action it records stands (OWASP's logging
/// guidance: a logging failure must not stop the application).
/// </summary>
public sealed class ModerationLog(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<IModerationLog> logger
) : IModerationLog
{
    public async Task RecordAsync(ModerationRecord record, CancellationToken ct)
    {
        try
        {
            await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

            dbCtx.ModerationActions.Add(
                new ModerationActionEntity
                {
                    Action = record.Action,
                    Source = record.Source,
                    ActorId = record.ActorId?.Value,
                    TargetPlayerId = record.TargetPlayerId?.Value,
                    RoomId = record.RoomId?.Value,
                    GuildId = record.GuildId,
                    Reason = Clip(record.Reason, ModerationActionEntity.REASON_MAX_LENGTH),
                    ExpiresAt = record.ExpiresAt,
                    Detail = Clip(record.Detail, ModerationActionEntity.DETAIL_MAX_LENGTH),
                }
            );

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Could not record moderation action {Action} from {Source} by {ActorId} on player {TargetPlayerId} in room {RoomId}: {Reason} {Detail}",
                record.Action,
                record.Source,
                record.ActorId,
                record.TargetPlayerId,
                record.RoomId,
                record.Reason,
                record.Detail
            );
        }
    }

    private static string? Clip(string? text, int max) =>
        text is null ? null
        : text.Length <= max ? text
        : text[..max];
}
