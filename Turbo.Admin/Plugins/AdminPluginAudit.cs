using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Plugins;

/// <summary>
/// Writes each plugin action taken in the panel to the command log as <c>plugin &lt;action&gt;
/// &lt;key&gt;</c>, from the <c>panel</c>, whoever takes it: unlike a command, which is logged
/// only for holders of <c>command.log</c>, a plugin change is server code changing, so every one
/// is kept, refusals included.
/// </summary>
public sealed class AdminPluginAudit(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<AdminPluginAudit> logger
)
{
    public const string COMMAND = "plugin";
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
                    Arguments = Truncate(arguments, CommandLogEntity.ARGUMENTS_MAX_LENGTH),
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
                "Failed to write the command log entry for plugin {Arguments} by {Player}",
                arguments,
                player
            );
        }
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];
}
