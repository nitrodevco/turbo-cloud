using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Primitives.Texts;

namespace Turbo.Rooms.Wired.Variables;

/// <summary>
/// The names a variable shows beside its number in the wired editor. The client writes a
/// connector straight into its table, so these are the hotel's own texts resolved here and not
/// keys; a hotel with no texts configured simply shows the number.
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

    /// <summary>
    /// Every id from zero up to <paramref name="maxId"/> the hotel has a text for. Ids nobody
    /// named are left out, so the editor shows a bare number for them.
    /// </summary>
    public static Dictionary<WiredVariableValue, string> ForIdRange(
        IHotelTextProvider texts,
        string keyFormat,
        int maxId
    )
    {
        var connectors = new Dictionary<WiredVariableValue, string>();

        for (var id = 0; id <= maxId; id++)
        {
            if (texts.TryGetText(string.Format(keyFormat, id), out var text))
                connectors[WiredVariableValue.Parse(id)] = text;
        }

        return connectors;
    }

    /// <summary>The same for a set of ids that is not a range, such as an enum's values.</summary>
    public static Dictionary<WiredVariableValue, string> ForIds(
        IHotelTextProvider texts,
        string keyFormat,
        IEnumerable<int> ids
    )
    {
        var connectors = new Dictionary<WiredVariableValue, string>();

        foreach (var id in ids)
        {
            if (texts.TryGetText(string.Format(keyFormat, id), out var text))
                connectors[WiredVariableValue.Parse(id)] = text;
        }

        return connectors;
    }
}
