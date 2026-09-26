using System;

namespace Turbo.Primitives.Texts;

/// <summary>
/// Whether a client string is a six-digit RGB colour. The dimmer and pet figures had one check
/// each; the dimmer's went through a hex number parse, which allows surrounding whitespace, so
/// "# ABCDE" passed. A colour is exactly six hex digits, nothing around them.
/// </summary>
public static class HexColor
{
    public const int DIGITS = 6;

    public static bool IsRgb(ReadOnlySpan<char> value)
    {
        if (value.Length != DIGITS)
            return false;

        foreach (var c in value)
        {
            if (!char.IsAsciiHexDigit(c))
                return false;
        }

        return true;
    }
}
