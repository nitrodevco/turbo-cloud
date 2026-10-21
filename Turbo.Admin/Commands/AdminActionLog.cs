using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Commands;

/// <summary>
/// Writes a panel action to the command log as <c>&lt;command&gt; &lt;arguments&gt;</c>, from the
/// <c>panel</c>, whoever takes it: unlike a command, which is logged only for holders of
/// <c>command.log</c>, these change the server (a plugin, a restart), cannot be undone (a group
/// deleted) or are what a command would do (a badge given or taken), so every one is kept.
/// </summary>
public sealed class AdminActionLog(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<AdminActionLog> logger
)
{
    /// <summary>A plugin installed, updated, switched, uninstalled or its admin route called.</summary>
    public const string PLUGIN = "plugin";

    /// <summary>A restart asked for or called off: <c>restart &lt;minutes&gt; [reason]</c> or <c>restart cancel</c>.</summary>
    public const string RESTART = "restart";

    /// <summary>A group deleted by staff: <c>group delete &lt;id&gt;</c>.</summary>
    public const string GROUP = "group";

    /// <summary>A badge given or taken on the Content page: <c>badge give|take &lt;code&gt; &lt;player id&gt;</c>.</summary>
    public const string BADGE = "badge";

    public const string SOURCE = "panel";

    public async Task WriteAsync(
        string command,
        PlayerId player,
        string arguments,
        CommandOutcome outcome,
        CancellationToken ct
    )
    {
        try
        {
            var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbScope = db.ConfigureAwait(false);

            db.CommandLogs.Add(
                new CommandLogEntity
                {
                    RoomEntityId = 0,
                    PlayerEntityId = player.Value,
                    Command = command,
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
                "Failed to write the command log entry for {Command} {Arguments} by {Player}",
                command,
                arguments,
                player
            );
        }
    }
}
