using System.Collections.Generic;

namespace Turbo.Primitives.Texts;

/// <summary>
/// Some of the hotel's texts, fetched together for code that reads them without waiting: a
/// command list's descriptions, a wired variable's names, an achievement pack's badge texts. It
/// holds the keys asked for that the hotel has; the default holds none.
/// </summary>
public readonly struct HotelTexts(IReadOnlyDictionary<string, string> texts)
{
    private readonly IReadOnlyDictionary<string, string>? _texts = texts;

    public static HotelTexts Empty => default;

    public int Count => _texts?.Count ?? 0;

    /// <summary>The text for a key; false when the hotel has none, or only an empty one.</summary>
    public bool TryGetText(string key, out string text)
    {
        text = string.Empty;

        if (_texts is null || !_texts.TryGetValue(key, out var found) || found.Length == 0)
            return false;

        text = found;

        return true;
    }
}
