using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Commands;

/// <summary>
/// A command that acts on the hotel rather than on the room it was typed in: it may name a player
/// who is offline or in another room, and it may be run from the server console. Implement
/// <see cref="IOperatorCommand{TArgs}"/>, not this. It runs outside the room's turn, so it can
/// await any grain without the room waiting on itself; the cost is that it cannot read the room's
/// live state, only the snapshot in <see cref="IOperatorExecutor.RoomPlayerIds"/>.
/// </summary>
public interface IOperatorCommand : ICommand
{
    ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        object arguments,
        CancellationToken ct
    );

    /// <summary>The room runs a room command, so an operator command is never given a room context.</summary>
    ValueTask<CommandResult> ICommand.ExecuteAsync(
        ICommandContext ctx,
        object arguments,
        CancellationToken ct
    ) => throw new NotSupportedException("An operator command does not run in a room's turn.");
}

/// <summary>An operator command with typed arguments, bound as for <see cref="ICommand{TArgs}"/>.</summary>
public interface IOperatorCommand<TArgs> : IOperatorCommand
    where TArgs : class
{
    Type ICommand.ArgumentsType => typeof(TArgs);

    ValueTask<CommandResult> IOperatorCommand.ExecuteAsync(
        IOperatorCommandContext ctx,
        object arguments,
        CancellationToken ct
    ) => ExecuteAsync(ctx, (TArgs)arguments, ct);

    ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        TArgs arguments,
        CancellationToken ct
    );
}
