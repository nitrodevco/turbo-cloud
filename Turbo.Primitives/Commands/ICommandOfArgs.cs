using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Commands;

/// <summary>
/// A command with typed arguments. <typeparamref name="TArgs"/> is a record whose primary
/// constructor lists the parameters in order: <c>IRoomPlayer</c>, <c>string</c>, <c>int</c>,
/// <c>long</c>, <c>bool</c>, an enum, or <see cref="RestOfLine"/> (last). A nullable type or a
/// default value makes a parameter optional. Core binds and validates them, resolves players,
/// and replies with generated usage when the line does not fit, so the command never indexes an
/// array. Use <c>NoArguments</c> for a command that takes none.
/// </summary>
public interface ICommand<TArgs> : ICommand
    where TArgs : class
{
    System.Type ICommand.ArgumentsType => typeof(TArgs);

    ValueTask<CommandResult> ICommand.ExecuteAsync(
        ICommandContext ctx,
        object arguments,
        CancellationToken ct
    ) => ExecuteAsync(ctx, (TArgs)arguments, ct);

    ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        TArgs arguments,
        CancellationToken ct
    );
}
