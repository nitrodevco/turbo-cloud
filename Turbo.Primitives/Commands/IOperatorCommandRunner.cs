using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Commands;

/// <summary>
/// Runs <see cref="IOperatorCommand"/>s: binds the line, lets a plugin veto, runs the command,
/// answers its result, and writes the use down. The room hands a matched line here, and the
/// console hands it every line it does not know itself.
/// </summary>
public interface IOperatorCommandRunner
{
    /// <summary>
    /// Runs a command. A player's node is checked here when <paramref name="checkNode"/> is set;
    /// the room has already checked it against its own copy and passes false.
    /// </summary>
    Task<CommandOutcome> RunAsync(
        CommandDescriptor descriptor,
        IOperatorExecutor executor,
        string argumentText,
        bool checkNode,
        CancellationToken ct
    );

    /// <summary>
    /// Runs a whole line (<c>ban Alice 7d spam</c>, with or without a leading colon) for an
    /// executor with no room, the console. False when no command has that name.
    /// </summary>
    Task<bool> TryRunLineAsync(string line, IOperatorExecutor executor, CancellationToken ct);
}
