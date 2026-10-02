using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Commands;

/// <summary>
/// Values a client may offer for a word parameter that names this source with
/// <see cref="SuggestAttribute"/>. Found in an assembly like a command, so a plugin's sources come
/// and go with it. Asked only for an executor who may use the command, so a source never tells a
/// player more than the command would.
/// </summary>
public interface ISuggestionSource
{
    /// <summary>The name a <see cref="SuggestAttribute"/> gives; unique across what is loaded.</summary>
    string Name { get; }

    /// <summary>
    /// At most <paramref name="limit"/> values that begin with <paramref name="prefix"/>,
    /// ignoring case, sorted.
    /// </summary>
    Task<IReadOnlyList<string>> SuggestAsync(
        CommandSuggestionContext context,
        string prefix,
        int limit,
        CancellationToken ct
    );
}
