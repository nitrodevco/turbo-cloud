using System.Text.RegularExpressions;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Pets.Enums;

namespace Turbo.Primitives.Pets;

/// <summary>
/// Pet name rules shared by the package naming dialog, purchases, nest breeding and bot names. A
/// name holding a word of the hotel's filter is refused rather than censored.
/// </summary>
public static partial class PetNames
{
    [GeneratedRegex(@"^[\p{L}\p{N} .\-]+$")]
    private static partial Regex AllowedCharactersRegex();

    public static PetNameValidationType Validate(
        string? name,
        int minLength,
        int maxLength,
        IWordFilter wordFilter
    )
    {
        var trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length < minLength)
            return PetNameValidationType.TooShort;

        if (trimmed.Length > maxLength)
            return PetNameValidationType.TooLong;

        if (!AllowedCharactersRegex().IsMatch(trimmed))
            return PetNameValidationType.InvalidCharacters;

        if (!wordFilter.IsClean(trimmed))
            return PetNameValidationType.Forbidden;

        return PetNameValidationType.Ok;
    }
}
