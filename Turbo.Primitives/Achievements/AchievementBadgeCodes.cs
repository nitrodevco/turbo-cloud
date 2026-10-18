using System;

namespace Turbo.Primitives.Achievements;

/// <summary>
/// How the client reads an achievement badge code for its texts: the base is the code without
/// its trailing digits (<c>ACH_Login3</c> is <c>ACH_Login</c>). Which badges are levels of one
/// achievement is that achievement's own list of codes, not this base: Habbo's
/// <c>ACH_Profile1</c> and <c>ACH_Profile2</c> share a base but are two achievements.
/// </summary>
public static class AchievementBadgeCodes
{
    /// <summary>The code without its trailing level number; the code itself when it has none.</summary>
    public static string BaseOf(string code) => code[..LevelStart(code)];

    private static int LevelStart(string code)
    {
        var start = code.Length;
        while (start > 0 && char.IsAsciiDigit(code[start - 1]))
            start--;
        return start;
    }
}
