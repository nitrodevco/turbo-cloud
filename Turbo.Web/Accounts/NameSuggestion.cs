using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Turbo.Primitives.Players;

namespace Turbo.Web.Accounts;

/// <summary>
/// A hotel name to offer someone signing up with Discord: their username or the name they show,
/// with what Turbo's names can't hold left out, cut to length, and a number added when it is
/// someone else's. They can still choose another.
/// </summary>
public static class NameSuggestion
{
    private const string ALLOWED = "-=?!@:.,_";
    private const string FALLBACK = "Player";

    /// <summary>The first name of the candidates that fits the rules and is free.</summary>
    public static string Suggest(DiscordCandidates discord, Func<string, bool> isTaken)
    {
        var bases = Bases(discord).ToList();

        // Each name as it is first, then numbered: their shown name beats their username plus 1.
        foreach (var candidate in bases)
        {
            if (PlayerNames.Check(candidate) is null && !isTaken(candidate))
                return candidate;
        }

        foreach (var candidate in bases)
        {
            if (Free(candidate, isTaken) is { } free)
                return free;
        }

        return Free(FALLBACK, isTaken) ?? FALLBACK + Random.Shared.Next(100_000, 999_999);
    }

    /// <summary>The name, or it with the lowest number that makes it free, within the length.</summary>
    private static string? Free(string name, Func<string, bool> isTaken)
    {
        if (PlayerNames.Check(name) is null && !isTaken(name))
            return name;

        for (var n = 1; n < 1000; n++)
        {
            var suffix = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var numbered =
                name[..Math.Min(name.Length, PlayerNames.MAX_LENGTH - suffix.Length)] + suffix;

            if (PlayerNames.Check(numbered) is null && !isTaken(numbered))
                return numbered;
        }

        return null;
    }

    private static IEnumerable<string> Bases(DiscordCandidates discord) =>
        new[] { discord.Username, discord.GlobalName }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => Fit(x!))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Only the characters a name may hold (spaces and the rest dropped), no leading @, at most the
    /// longest name, and long enough, padded with digits when it is not.
    /// </summary>
    public static string Fit(string text)
    {
        var kept = new StringBuilder();

        foreach (var c in text.Trim())
        {
            if (char.IsAsciiLetterOrDigit(c) || ALLOWED.Contains(c))
                kept.Append(c);
        }

        var name = kept.ToString().TrimStart('@');

        if (name.Length > PlayerNames.MAX_LENGTH)
            name = name[..PlayerNames.MAX_LENGTH];

        if (name.Length is > 0 and < PlayerNames.MIN_LENGTH)
            name = name.PadRight(PlayerNames.MIN_LENGTH, '1');

        return name;
    }
}
