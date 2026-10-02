using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record UnbanArguments(PlayerTarget Who);

/// <summary>
/// <c>:unban name</c>. Lifts the ban in force. The row stays, stamped with who lifted it and
/// when, so the history of a player's sanctions is not lost by forgiving them.
/// </summary>
[Command(
    "unban",
    Description = "Lift a player's hotel ban",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.UNBAN)]
public sealed class UnbanCommand(ISanctionService sanctionService)
    : IOperatorCommand<UnbanArguments>
{
    private const string UNBANNED = "unbanned";
    private const string NOT_BANNED = "not_banned";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [UNBANNED] = "%0% is no longer banned.",
            [NOT_BANNED] = "%0% is not banned.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        UnbanArguments arguments,
        CancellationToken ct
    )
    {
        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var target = selection.Players[0];

        if (!await sanctionService.UnbanAsync(target.Id, ctx.Executor.PlayerId, ct))
            return CommandResult.Fail(NOT_BANNED, target.Name);

        await ctx.NotifyAsync(
            target.Id,
            "command.unban.notice",
            "Your hotel ban has been lifted.",
            [],
            ct
        );
        return CommandResult.Done(UNBANNED, target.Name);
    }
}
