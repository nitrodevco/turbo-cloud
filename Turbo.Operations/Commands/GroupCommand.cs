using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

/// <summary>
/// <c>:group add|remove name group [duration]</c>. Promotes a player into a permission group, or
/// takes them out, through the same grains and audit as the <c>perm</c> console command, so
/// promoting someone does not need the console. A group given with a duration ends by itself.
/// Whoever runs it must also hold <c>permissions.manage</c>, and goes through the same
/// <see cref="IPermissionEditService"/> as the admin panel: only groups lighter than their own
/// heaviest, and only for players lighter than them. The console may hand out any group.
/// </summary>
[Command(
    "group",
    Description = "Add a player to a permission group, or remove them",
    Category = CommandCategories.ADMINISTRATION
)]
[RequiresPermission(PermissionNodes.Command.GROUP)]
public sealed class GroupCommand(IPermissionEditService permissions)
    : IOperatorCommand<GroupArguments>
{
    private const string ADDED = "added";
    private const string REMOVED = "removed";
    private const string UNCHANGED = "unchanged";
    private const string NOT_MEMBER = "not_member";
    private const string UNKNOWN_GROUP = "unknown_group";
    private const string PROTECTED = "protected";
    private const string NOT_ALLOWED = "not_allowed";
    private const string GROUP_TOO_HEAVY = "group_too_heavy";
    private const string PLAYER_TOO_HEAVY = "player_too_heavy";
    private const string NEEDS_SUPERUSER = "needs_superuser";
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
            [GROUP_TOO_HEAVY] =
                "Only groups lighter than your heaviest are yours to hand out, and %1% is not.",
            [NEEDS_SUPERUSER] =
                "The group %1% gives permissions.superuser, so only a superuser can hand it out.",
            [PLAYER_TOO_HEAVY] =
                "%0% is in a group as heavy as your heaviest or heavier, so their groups are not yours to change.",
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
        var group = arguments.Group.ToLowerInvariant();

        // A membership is permanent or temporary, and which one a removal means is not typed:
        // whichever is there, the permanent one first.
        var change = arguments is GroupAddArguments add
            ? await permissions.AddToGroupAsync(
                ctx.Executor.PlayerId,
                target.Id,
                group,
                add.Duration?.Span,
                PermissionExpiryModeType.Replace,
                ct
            )
            : await permissions.RemoveFromGroupAsync(
                ctx.Executor.PlayerId,
                target.Id,
                group,
                null,
                ct
            );

        if (change.IsRefused)
            return change.Refusal switch
            {
                PermissionEditRefusal.GroupTooHeavy => CommandResult.Fail(
                    GROUP_TOO_HEAVY,
                    target.Name,
                    group
                ),
                PermissionEditRefusal.NeedsSuperuser => CommandResult.Fail(
                    NEEDS_SUPERUSER,
                    target.Name,
                    group
                ),
                PermissionEditRefusal.PlayerTooHeavy => CommandResult.Fail(
                    PLAYER_TOO_HEAVY,
                    target.Name,
                    group
                ),
                _ => CommandResult.Fail(NOT_ALLOWED),
            };

        if (change.Notice is { } notice)
            await ctx.NotifyAsync(
                target.Id,
                notice.TextKey,
                notice.DefaultText,
                notice.Parameters,
                ct
            );

        return Describe(
            change.Result,
            arguments is GroupAddArguments ? ADDED : REMOVED,
            target.Name,
            group
        );
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
