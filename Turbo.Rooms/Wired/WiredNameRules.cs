using System;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>
/// The rule Habbo holds a name a player types into wired to (a variable's, a placeholder's): 1 to
/// 40 characters, each a letter, a digit or "_". It is the client's own check for link keys and
/// placeholders (<c>LinkDataframeCodec.keyError</c> / <c>placeholderError</c>, AS3
/// WIN63-202610131318: length 1..40, then <c>/^\w+$/</c>, whose <c>\w</c> is ASCII only), and the
/// hotel texts say the same of variable names (<c>wiredfurni.error.variables.name_length</c> /
/// <c>name_syntax</c>).
/// </summary>
public static class WiredNameRules
{
    public const int MAX_LENGTH = 40;

    public static bool HasValidLength(string name) => name.Length is > 0 and <= MAX_LENGTH;

    public static bool HasValidSyntax(string name)
    {
        foreach (var c in name)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_'))
                return false;
        }

        return name.Length > 0;
    }

    /// <summary>
    /// What is wrong with a placeholder add-on's string param, as the error text the editor
    /// shows; null when nothing is. The editor sends the name, then a tab and the delimiter when
    /// "show all" is picked (<c>UsernamePlaceholderAddon</c> / <c>VariablePlaceholderAddon</c>
    /// <c>readStringParamFromForm</c>, AS3 WIN63-202610131318); the capturer sends the name only.
    /// The name is checked as <c>LinkDataframeCodec.placeholderError</c> checks one, trigger_text
    /// included (inference for the add-ons: the client checks it for link placeholders, and the
    /// hotel text names it reserved); an empty delimiter with "show all" is refused
    /// (<c>wiredfurni.error.placeholder.delimiter_empty</c>; inference: the condition is not
    /// in the client).
    /// </summary>
    public static string? PlaceholderError(string? stringParam, bool showsAll)
    {
        var text = stringParam ?? string.Empty;
        var tab = text.IndexOf('\t');
        var name = tab < 0 ? text : text[..tab];

        if (!HasValidLength(name))
            return WiredSaveErrors.PLACEHOLDER_NAME_LENGTH;

        if (!HasValidSyntax(name))
            return WiredSaveErrors.PLACEHOLDER_NAME_SYNTAX;

        if (name.Equals(WiredPlaceholderText.TRIGGER_TEXT_NAME, StringComparison.OrdinalIgnoreCase))
            return WiredSaveErrors.PLACEHOLDER_RESERVED;

        if (showsAll && (tab < 0 || tab == text.Length - 1))
            return WiredSaveErrors.PLACEHOLDER_DELIMITER_EMPTY;

        return null;
    }
}
