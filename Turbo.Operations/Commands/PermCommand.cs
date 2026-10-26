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
/// <c>:perm user name set|unset|unsettemp node [true|false] [duration]</c> grants, denies or takes
/// away a node on the player themselves, as the console's <c>perm user</c> does, for whoever also
/// holds <c>permissions.manage</c>, through the same <see cref="IPermissionEditService"/> as the
/// admin panel: only players lighter than them, and only nodes they hold (a superuser: anyone,
/// themselves too). Groups are <c>:group</c>'s.
/// </summary>
[Command(
    "perm",
    Description = "Check why a player holds a permission, or does not",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Command.PERM)]
public sealed class PermCommand(IGrainFactory grainFactory, IPermissionEditService permissions)
    : IOperatorCommand<PermArguments>
{
    private const string CHECKED = "checked";
    private const string SET = "set";
    private const string UNSET = "unset";
    private const string UNCHANGED = "unchanged";
    private const string NOT_SET = "not_set";
    private const string INVALID = "invalid";
    private const string RESERVED = "reserved";
    private const string NOT_ALLOWED = "not_allowed";
    private const string NODE_NOT_HELD = "node_not_held";
    private const string PLAYER_TOO_HEAVY = "player_too_heavy";
    private const string YOURSELF = "yourself";
    private const string WOULD_LOSE_SUPERUSER = "would_lose_superuser";
    private const string FAILED = "failed";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [SET] = "%0% now has %1% = %2%%3%.",
            [UNSET] = "Took %1% off %0%.",
            [UNCHANGED] = "%0% already has %1% = %2%%3%.",
            [NOT_SET] = "%1% is not set on %0% that way.",
            [INVALID] = "%1% is not a node.",
            [RESERVED] = "%1% follows from the player's groups: use :group instead.",
            [NOT_ALLOWED] = "You need permission to manage permissions to use that.",
            [NODE_NOT_HELD] = "Only nodes you hold are yours to hand out, and %1% is not.",
            [PLAYER_TOO_HEAVY] =
                "%0% is in a group as heavy as your heaviest or heavier, so their permissions are not yours to change.",
            [YOURSELF] = "Only a superuser changes their own permissions.",
            [WOULD_LOSE_SUPERUSER] =
                "That would take permissions.superuser from you. Make someone else a superuser first, or use the server console.",
            [FAILED] = "%0% could not be changed (%2%).",
        };

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

        if (arguments is PermUserArguments user)
            return await ChangeAsync(ctx, target, user, ct);

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

    private async ValueTask<CommandResult> ChangeAsync(
        IOperatorCommandContext ctx,
        ResolvedPlayer target,
        PermUserArguments arguments,
        CancellationToken ct
    )
    {
        if (!await ctx.Executor.HasAsync(PermissionNodes.Permissions.MANAGE, ct))
            return CommandResult.Fail(NOT_ALLOWED);

        var node = arguments.Node.ToLowerInvariant();
        var change = arguments.Action switch
        {
            PermUserAction.Set => await permissions.SetNodeAsync(
                ctx.Executor.PlayerId,
                target.Id,
                node,
                arguments.Value,
                arguments.Duration?.Span,
                PermissionExpiryModeType.Replace,
                ct
            ),
            _ => await permissions.UnsetNodeAsync(
                ctx.Executor.PlayerId,
                target.Id,
                node,
                arguments.Action == PermUserAction.Unsettemp,
                ct
            ),
        };

        if (change.IsRefused)
            return change.Refusal switch
            {
                PermissionEditRefusal.NodeNotHeld => CommandResult.Fail(
                    NODE_NOT_HELD,
                    target.Name,
                    node
                ),
                PermissionEditRefusal.PlayerTooHeavy when target.Id == ctx.Executor.PlayerId =>
                    CommandResult.Fail(YOURSELF),
                PermissionEditRefusal.PlayerTooHeavy => CommandResult.Fail(
                    PLAYER_TOO_HEAVY,
                    target.Name,
                    node
                ),
                PermissionEditRefusal.WouldLoseSuperuser => CommandResult.Fail(
                    WOULD_LOSE_SUPERUSER
                ),
                _ => CommandResult.Fail(NOT_ALLOWED),
            };

        var value = arguments.Value ? "true" : "false";
        var until = arguments.Duration is { } duration ? $" for {duration}" : string.Empty;

        return change.Result switch
        {
            PermissionChangeResultType.Changed => arguments.Action == PermUserAction.Set
                ? CommandResult.Done(SET, target.Name, node, value, until)
                : CommandResult.Done(UNSET, target.Name, node),
            PermissionChangeResultType.Unchanged => CommandResult.Fail(
                UNCHANGED,
                target.Name,
                node,
                value,
                until
            ),
            PermissionChangeResultType.NotFound => CommandResult.Fail(NOT_SET, target.Name, node),
            PermissionChangeResultType.Invalid => CommandResult.Fail(INVALID, target.Name, node),
            PermissionChangeResultType.ReservedNode => CommandResult.Fail(
                RESERVED,
                target.Name,
                node
            ),
            _ => CommandResult.Fail(FAILED, target.Name, node, change.Result.ToString()),
        };
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
