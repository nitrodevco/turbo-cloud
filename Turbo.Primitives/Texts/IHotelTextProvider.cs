using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Texts;

/// <summary>
/// The hotel's own texts, the ones the client shows, kept in the database (the gamedata's
/// external texts). The server needs them where it sends the client a word rather than a key: a
/// wired variable's text connectors, a command's reply, a ban message. Only the keys asked for are
/// read, never the whole list; each has any <c>${other.key}</c> it is made of resolved.
/// </summary>
public interface IHotelTextProvider
{
    /// <summary>The texts for these keys, those the hotel has.</summary>
    public Task<HotelTexts> GetTextsAsync(IEnumerable<string> keys, CancellationToken ct);

    /// <summary>
    /// Every text whose key starts with this, for a family read whole (<c>fx_</c>, the effects'
    /// names): only the keys the hotel has come back, however many ids could have one.
    /// </summary>
    public Task<HotelTexts> GetTextsByPrefixAsync(string prefix, CancellationToken ct);

    /// <summary>The text for a key; null when the hotel has none, or only an empty one.</summary>
    public Task<string?> GetTextAsync(string key, CancellationToken ct);

    /// <summary>Forgets the texts read so far: the next ask reads them again.</summary>
    public void Invalidate();
}
