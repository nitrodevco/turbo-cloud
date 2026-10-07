using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.Texts;

namespace Turbo.Rooms.Wired.Variables;

/// <summary>
/// The names a variable shows beside its number in the wired editor. The client writes a
/// connector straight into its table, so these are the hotel's own texts resolved here and not
/// keys, read from the database
/// only for the ids a variable names; a hotel with no text for an id simply shows the number.
///
/// The key each id is named by is the client's: <c>handitem{id}</c>, <c>fx_{id}</c>, and the
/// wired editor's own lists for dances and signs.
/// </summary>
internal static class WiredTextConnectors
{
    public const string HAND_ITEM_KEY = "handitem{0}";
    public const string EFFECT_KEY = "fx_{0}";
    public const string DANCE_KEY = "wiredfurni.params.action.dance.{0}";
    public const string SIGN_KEY = "wiredfurni.params.action.sign.{0}";

    /// <summary>What every key of this format starts with: the family of texts to read for it.</summary>
    public static string Prefix(string keyFormat) => keyFormat[..keyFormat.IndexOf('{')];

    /// <summary>
    /// Every one of these ids the hotel has a text for. Ids nobody named are left out, so the
    /// editor shows a bare number for them.
    /// </summary>
    public static Dictionary<WiredVariableValue, string> ForIds(
        HotelTexts texts,
        string keyFormat,
        IEnumerable<int> ids
    )
    {
        var connectors = new Dictionary<WiredVariableValue, string>();

        foreach (var id in ids)
        {
            if (texts.TryGetText(Key(keyFormat, id), out var text))
                connectors[WiredVariableValue.Parse(id)] = text;
        }

        return connectors;
    }

    /// <summary>The ids from zero up to <paramref name="maxId"/>.</summary>
    public static IEnumerable<int> UpTo(int maxId) => Enumerable.Range(0, Math.Max(0, maxId + 1));

    private static string Key(string keyFormat, int id) =>
        string.Format(CultureInfo.InvariantCulture, keyFormat, id);
}
