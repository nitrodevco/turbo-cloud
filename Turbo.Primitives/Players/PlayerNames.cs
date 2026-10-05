using System;
using System.Text.RegularExpressions;

namespace Turbo.Primitives.Players;

/// <summary>
/// What a new player may be called: the characters the client's own name check allows, no
/// spaces, and not starting with <c>@</c>, which commands read as a group of players
/// (<c>@room</c>, <c>@online</c>).
/// </summary>
public static partial class PlayerNames
{
    public const int MIN_LENGTH = 3;
    public const int MAX_LENGTH = 15;

    [GeneratedRegex(@"^[A-Za-z0-9\-=?!@:.,_]+$")]
    private static partial Regex AllowedCharactersRegex();

    /// <summary>Why a name can't be used, in words; null when it can.</summary>
    public static string? Check(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length < MIN_LENGTH || trimmed.Length > MAX_LENGTH)
            return $"A name is {MIN_LENGTH} to {MAX_LENGTH} characters.";

        if (!AllowedCharactersRegex().IsMatch(trimmed))
            return "A name is letters, digits and - = ? ! @ : . , _ only, with no spaces.";

        return trimmed.StartsWith('@')
            ? "A name can't start with @, which commands read as a group of players."
            : null;
    }

    /// <summary>Whether two names are the same name, as the database's collation compares them.</summary>
    public static bool Same(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
