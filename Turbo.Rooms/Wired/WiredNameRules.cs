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
}
