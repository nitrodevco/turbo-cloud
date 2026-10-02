using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Operations.Commands;

/// <summary>
/// <c>:perm check name node</c>. Why a player does or does not hold a node: what decided it, and
/// what that beat. For the question every support case starts with, "why can't they do that".
/// Changing permissions stays with <c>:group</c> and the console's <c>perm</c>.
/// </summary>
[Command(
    "perm",
    Description = "Check why a player holds a permission, or does not",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Command.PERM)]
public sealed class PermCommand(IGrainFactory grainFactory) : IOperatorCommand<PermArguments>
{
    private const string CHECKED = "checked";

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        PermArguments arguments,
        CancellationToken ct
    )
    {
        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var target = selection.Players[0];
        var check = await grainFactory
            .GetPlayerPermissionGrain(target.Id)
            .ExplainAsync(arguments.Node, ct);

        var lines = new List<string>
        {
            $"{target.Name}: {check.Node} is {(check.Granted ? "granted" : "denied")}"
                + (check.IsRegistered ? string.Empty : " (no such node is registered)"),
            check.Decision is { } decision ? $"Decided by {Describe(decision)}"
            : check.Granted ? "Decided by the node's default: it is granted to everyone"
            : "Decided by nothing: no group or player sets it, and it is not granted by default",
        };

        foreach (var beaten in check.Overridden)
            lines.Add($"Overrides {Describe(beaten)}");

        await ctx.Executor.NoticeAsync(lines, ct);

        return CommandResult.Done(CHECKED);
    }

    private static string Describe(PermissionAssignmentSourceSnapshot source)
    {
        var from =
            source.SourceType == PermissionSourceType.Player
                ? "the player's own"
                : $"group {source.GroupName}";
        var via = source.Path.Length > 1 ? $" via {string.Join(" > ", source.Path)}" : string.Empty;
        var until = source.ExpiresAt is { } ends
            ? $" until {ends.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)}"
            : string.Empty;

        return $"{from} {source.Node} = {(source.Value ? "grant" : "deny")}{via}{until}";
    }
}
