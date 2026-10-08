using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Moderation;

/// <summary>
/// The hotel's word filter: every text a player types that is shown to anyone goes through it
/// before it is stored or sent. Free text (chat, messages, descriptions, inscriptions) has the
/// words replaced; a name (a pet's, a bot's, a new player's) that holds one is refused instead,
/// as the hotel cannot pick a name for them. Matching is <see cref="FilterWords"/>.
/// </summary>
public interface IWordFilter
{
    /// <summary>What a filtered word is replaced with, here and in each room's own filter.</summary>
    public string Replacement { get; }

    /// <summary>The text with the hotel's words replaced.</summary>
    public string Filter(string text);

    /// <summary>
    /// The text with the hotel's words and these as well replaced, in one pass: a room's chat
    /// goes through the hotel's list and then the room owner's.
    /// </summary>
    public string Filter(string text, IReadOnlySet<string> extraWords);

    /// <summary>Whether the text holds none of the hotel's words.</summary>
    public bool IsClean(string text);

    public Task ReloadAsync(CancellationToken ct);
}
