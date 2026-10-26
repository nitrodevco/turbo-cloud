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

    /// <summary>A player marked as a system account or a person again: <c>player system &lt;id&gt; on|off</c>.</summary>
    public const string PLAYER = "player";

    /// <summary>A room given to another player: <c>room owner &lt;room id&gt; &lt;player id&gt;</c>.</summary>
    public const string ROOM = "room";

    /// <summary>
    /// A change to the navigator's Public tab: <c>public category add|save|delete|order ...</c> or
    /// <c>public room add|move|order|remove &lt;category id&gt; ...</c>.
    /// </summary>
    public const string NAVIGATOR = "navigator";

    public const string SOURCE = "panel";

    /// <summary>The source of a change a plugin made through the hotel API, logged as no player (0).</summary>
    public const string PLUGIN_SOURCE = "plugin";

    public Task WriteAsync(
        string command,
        PlayerId player,
        string arguments,
        CommandOutcome outcome,
        CancellationToken ct
    ) => WriteAsync(command, player, arguments, outcome, SOURCE, ct);

    public async Task WriteAsync(
        string command,
        PlayerId player,
        string arguments,
        CommandOutcome outcome,
        string source,
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
                    Source = source,
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
