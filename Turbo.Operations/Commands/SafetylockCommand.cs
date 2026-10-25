using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Accounts;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record SafetylockArguments(PlayerTarget Who);

/// <summary>
/// <c>:safetylock name</c>. Puts Habbo's account safety lock on a player's account until it is
/// lifted (<see cref="IPlayerSafetyLockService"/>); their client is told at once, which shows its
/// own lock notice.
/// </summary>
[Command(
    "safetylock",
    Description = "Safety lock a player's account",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.SAFETYLOCK)]
public sealed class SafetylockCommand(IGrainFactory grainFactory, IPlayerSafetyLockService locks)
    : IOperatorCommand<SafetylockArguments>
{
    private const string LOCKED = "locked";
    private const string ALREADY = "already";
    private const string PROTECTED = "protected";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [LOCKED] = "%0%'s account is safety locked.",
            [ALREADY] = "%0%'s account is already safety locked.",
            [PROTECTED] = "You can't safety lock %0%.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        SafetylockArguments arguments,
        CancellationToken ct
    )
    {
        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var target = selection.Players[0];

        if (
            await OperatorGuards.IsProtectedAsync(
                grainFactory,
                ctx.Executor,
                target.Id,
                PermissionNodes.Command.SAFETYLOCK,
                ct
            )
        )
            return CommandResult.Fail(PROTECTED, target.Name);

        return await locks.SetLockedAsync(target.Id, true, ct)
            ? CommandResult.Done(LOCKED, target.Name)
            : CommandResult.Fail(ALREADY, target.Name);
    }
}
