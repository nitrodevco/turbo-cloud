using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Accounts;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record UnsafetylockArguments(PlayerTarget Who);

/// <summary>
/// <c>:unsafetylock name</c>. Lifts the account safety lock, however it was put on. Whoever may
/// lock may lift.
/// </summary>
[Command(
    "unsafetylock",
    Description = "Lift the safety lock from a player's account",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.SAFETYLOCK)]
public sealed class UnsafetylockCommand(IPlayerSafetyLockService locks)
    : IOperatorCommand<UnsafetylockArguments>
{
    private const string LIFTED = "lifted";
    private const string NOT_LOCKED = "not_locked";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [LIFTED] = "Lifted the safety lock from %0%'s account.",
            [NOT_LOCKED] = "%0%'s account is not safety locked.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        UnsafetylockArguments arguments,
        CancellationToken ct
    )
    {
        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var target = selection.Players[0];

        return await locks.SetLockedAsync(target.Id, false, ct)
            ? CommandResult.Done(LIFTED, target.Name)
            : CommandResult.Fail(NOT_LOCKED, target.Name);
    }
}
