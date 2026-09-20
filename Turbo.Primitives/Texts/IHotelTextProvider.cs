using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Texts;

/// <summary>
/// The hotel's own texts, the ones the client would show. The server needs them where it sends
/// the client a word rather than a key: a wired variable's text connectors are written straight
/// into the editor's table, so a key would be shown as a key.
/// </summary>
public interface IHotelTextProvider
{
    /// <summary>
    /// The text for a key, with any <c>${other.key}</c> it points at resolved. False when the
    /// hotel has no text for it, which is the normal answer for an id nobody named.
    /// </summary>
    public bool TryGetText(string key, out string text);

    /// <summary>Reads the texts again, from wherever the hotel keeps them.</summary>
    public Task ReloadAsync(CancellationToken ct = default);
}
