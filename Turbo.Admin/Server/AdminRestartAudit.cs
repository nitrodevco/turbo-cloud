using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Server;

/// <summary>
/// Writes each restart asked for or called off in the panel to the command log as <c>restart
/// &lt;minutes&gt; [reason]</c> or <c>restart cancel</c>, from the <c>panel</c>, whoever asks:
/// unlike a command, which is logged only for holders of <c>command.log</c>, a restart sends the
/// whole hotel home, so every one is kept, refusals included.
/// </summary>
public sealed class AdminRestartAudit(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<AdminRestartAudit> logger
)
{
    public const string COMMAND = "restart";
    public const string SOURCE = "panel";

    public async Task WriteAsync(
        PlayerId player,
        string arguments,
        CommandOutcome outcome,
        CancellationToken ct
    )
    {
        try
        {
            await using var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

            db.CommandLogs.Add(
                new CommandLogEntity
                {
                    RoomEntityId = 0,
                    PlayerEntityId = player.Value,
                    Command = COMMAND,
                    Arguments =
                        arguments.Length <= CommandLogEntity.ARGUMENTS_MAX_LENGTH
                            ? arguments
                            : arguments[..CommandLogEntity.ARGUMENTS_MAX_LENGTH],
                    Outcome = CommandTelemetry.Name(outcome),
                    ExecutionId = Guid.NewGuid(),
                    Source = SOURCE,
                }
            );

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to write the command log entry for restart {Arguments} by {Player}",
                arguments,
                player
            );
        }
    }
}
