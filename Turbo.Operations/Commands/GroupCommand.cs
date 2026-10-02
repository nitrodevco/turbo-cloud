using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

/// <summary>
/// <c>:group add|remove name group [duration]</c>. Promotes a player into a permission group, or
/// takes them out, through the same grains and audit as the <c>perm</c> console command, so
/// promoting someone does not need the console. A group given with a duration ends by itself.
/// Whoever runs it must also hold <c>permissions.manage</c>: <c>command.group</c> alone is not
/// enough to hand out a group above one's own.
/// </summary>
[Command(
    "group",
    Description = "Add a player to a permission group, or remove them",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Command.GROUP)]
public sealed class GroupCommand(IGrainFactory grainFactory, TimeProvider timeProvider)
    : IOperatorCommand<GroupArguments>
{
    private const string ADDED = "added";
    private const string REMOVED = "removed";
    private const string UNCHANGED = "unchanged";
    private const string NOT_MEMBER = "not_member";
    private const string UNKNOWN_GROUP = "unknown_group";
    private const string PROTECTED = "protected";
    private const string NOT_ALLOWED = "not_allowed";
    private const string FAILED = "failed";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [ADDED] = "%0% is now in the group %1%.",
            [REMOVED] = "Removed a %1% group assignment from %0%.",
            [UNCHANGED] = "%0% is already in the group %1%.",
            [NOT_MEMBER] = "%0% is not in the group %1%.",
            [UNKNOWN_GROUP] = "There is no group called %1%.",
            [PROTECTED] = "Nobody joins or leaves the group %1% by hand.",
            [NOT_ALLOWED] = "You need permission to manage permissions to use that.",
            [FAILED] = "%0% could not be changed (%2%).",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        GroupArguments arguments,
        CancellationToken ct
    )
    {
        if (!await ctx.Executor.HasAsync(PermissionNodes.Permissions.MANAGE, ct))
            return CommandResult.Fail(NOT_ALLOWED);

        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var target = selection.Players[0];
        var grain = grainFactory.GetPlayerPermissionGrain(target.Id);
        var group = arguments.Group.ToLowerInvariant();

        if (arguments is GroupAddArguments add)
        {
            var expiresAt = add.Duration?.EndsAt(timeProvider.GetUtcNow().UtcDateTime);

            var change = await grain.AddGroupAsync(
                group,
                expiresAt,
                PermissionExpiryModeType.Replace,
                ctx.Executor.PlayerId,
                ct
            );
            if (change == PermissionChangeResultType.Changed)
                await ctx.NotifyAsync(
                    target.Id,
                    "command.group.add.notice",
                    "You have been assigned to the group %0% (%1%).",
                    [group, add.Duration?.ToString() ?? "permanent"],
                    ct
                );
            return Describe(change, ADDED, target.Name, group);
        }

        // A membership is permanent or temporary, and which one this is is not typed: remove
        // whichever is there, the permanent one first.
        var result = await grain.RemoveGroupAsync(group, false, ctx.Executor.PlayerId, ct);

        if (result == PermissionChangeResultType.NotFound)
            result = await grain.RemoveGroupAsync(group, true, ctx.Executor.PlayerId, ct);

        if (result == PermissionChangeResultType.Changed)
            await ctx.NotifyAsync(
                target.Id,
                "command.group.remove.notice",
                "A %0% group assignment has been removed from your account.",
                [group],
                ct
            );
        return Describe(result, REMOVED, target.Name, group);
    }

    private static CommandResult Describe(
        PermissionChangeResultType result,
        string done,
        string player,
        string group
    ) =>
        result switch
        {
            PermissionChangeResultType.Changed => CommandResult.Done(done, player, group),
            PermissionChangeResultType.Unchanged => CommandResult.Fail(UNCHANGED, player, group),
            PermissionChangeResultType.NotFound => CommandResult.Fail(NOT_MEMBER, player, group),
            PermissionChangeResultType.UnknownGroup => CommandResult.Fail(
                UNKNOWN_GROUP,
                player,
                group
            ),
            PermissionChangeResultType.ProtectedGroup => CommandResult.Fail(
                PROTECTED,
                player,
                group
            ),
            _ => CommandResult.Fail(FAILED, player, group, result.ToString()),
        };
}
