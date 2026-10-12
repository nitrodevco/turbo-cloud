using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record HistoryArguments(PlayerTarget Who);

/// <summary>
/// <c>:history name</c>. The last moderation a player has had, newest first: what, by whom, when,
/// why and until when. What a moderator reads before deciding what to do next, without leaving
/// the game for the panel.
/// </summary>
[Command(
    "history",
    Description = "Read a player's moderation history",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.HISTORY)]
public sealed class HistoryCommand(IDbContextFactory<TurboDbContext> dbCtxFactory)
    : IOperatorCommand<HistoryArguments>
{
    /// <summary>As many as a notice shows comfortably; the panel has the rest.</summary>
    public const int SHOWN = 15;

    private const string REPORTED = "reported";
    private const string CLEAN = "clean";
    private const string DATE_FORMAT = "yyyy-MM-dd HH:mm";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string> { [CLEAN] = "%0% has no moderation history." };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        HistoryArguments arguments,
        CancellationToken ct
    )
    {
        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var target = selection.Players[0];

        await using var db = await dbCtxFactory.CreateDbContextAsync(ct);

        var actions = await db
            .ModerationActions.AsNoTracking()
            .Where(x => x.TargetPlayerId == target.Id.Value)
            .OrderByDescending(x => x.Id)
            .Take(SHOWN)
            .ToListAsync(ct);

        if (actions.Count == 0)
            return CommandResult.Done(CLEAN, target.Name);

        var actorIds = actions.Select(x => x.ActorId).OfType<int>().Distinct().ToList();
        var actors = await db
            .Players.AsNoTracking()
            .Where(x => actorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var lines = new List<string> { $"{target.Name}: the last {actions.Count}, newest first" };

        foreach (var x in actions)
        {
            var by =
                x.ActorId is { } actor ? actors.GetValueOrDefault(actor, $"player {actor}")
                : x.Source == ModerationSource.Console ? "console"
                : "hotel";
            var until = x.ExpiresAt is { } ends
                ? $" until {ends.ToString(DATE_FORMAT, CultureInfo.InvariantCulture)}"
                : string.Empty;
            var why = string.IsNullOrEmpty(x.Reason) ? string.Empty : $": {x.Reason}";

            lines.Add(
                $"{x.CreatedAt.ToString(DATE_FORMAT, CultureInfo.InvariantCulture)} {x.Action} by {by}{until}{why}"
            );
        }

        await ctx.Executor.NoticeAsync(lines, ct);

        return CommandResult.Done(REPORTED);
    }
}
