using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Commands;

/// <summary>
/// A chat command. Implement <see cref="ICommand{TArgs}"/>, not this: it is what the registry
/// finds in an assembly. A command is built once, with its plugin's services, and is shared by
/// every executor, so it keeps no state of its own.
/// </summary>
public interface ICommand
{
    /// <summary>The arguments record the command declares, bound from the line by core.</summary>
    System.Type ArgumentsType { get; }

    /// <summary>
    /// The text of each status the command returns, by status, used when the hotel's texts hold
    /// no <c>command.&lt;name&gt;.&lt;status&gt;</c>. <c>%0%</c>, <c>%1%</c>... take the result's
    /// parameters.
    /// </summary>
    IReadOnlyDictionary<string, string> DefaultTexts => NoTexts.Instance;

    ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        object arguments,
        CancellationToken ct
    );
}
