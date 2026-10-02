using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Commands;

/// <summary>
/// Answers a client's request for the values of one parameter (<c>chat.commands</c>). Empty, never
/// an error, for anything it will not answer: a command the player may not use, a parameter with
/// nothing to offer, a request over the limit.
/// </summary>
public interface ICommandSuggestionService
{
    Task<ImmutableArray<string>> SuggestAsync(
        PlayerId playerId,
        string command,
        int parameter,
        string prefix,
        CancellationToken ct,
        string syntax = "",
        string argumentText = ""
    );
}
