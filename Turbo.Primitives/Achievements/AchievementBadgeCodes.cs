using System;

namespace Turbo.Primitives.Achievements;

/// <summary>
/// The levels of one achievement share a badge base followed by the level number
/// (<c>ACH_Login1</c> .. <c>ACH_Login20</c>), as the client reads them. Only an exact base plus
/// digits is a level of that base, so <c>ACH_Login</c> never claims <c>ACH_LoginStreak5</c>.
/// </summary>
public static class AchievementBadgeCodes
{
    /// <summary>The code without its trailing level number; the code itself when it has none.</summary>
    public static string BaseOf(string code) => code[..LevelStart(code)];

    /// <summary>The base of a code that ends in a level number; false for a code without one.</summary>
    public static bool TryGetBase(string code, out string badgeBase)
    {
        var start = LevelStart(code);
        badgeBase = code[..start];
        return start > 0 && start < code.Length;
    }

    /// <summary>Whether the code is the base followed by a level number and nothing else.</summary>
    public static bool IsLevelOf(string badgeBase, string code) =>
        code.Length > badgeBase.Length
        && code.StartsWith(badgeBase, StringComparison.Ordinal)
        && code.AsSpan(badgeBase.Length).IndexOfAnyExceptInRange('0', '9') < 0;

    /// <summary>The trailing level number; zero when the code has none or it does not fit.</summary>
    public static int LevelOf(string code) =>
        int.TryParse(code.AsSpan(LevelStart(code)), out var level) ? level : 0;

    private static int LevelStart(string code)
    {
        var start = code.Length;
        while (start > 0 && char.IsAsciiDigit(code[start - 1]))
            start--;
        return start;
    }
}
