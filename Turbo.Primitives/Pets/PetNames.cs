using System.Text.RegularExpressions;
using Turbo.Primitives.Pets.Enums;

namespace Turbo.Primitives.Pets;

/// <summary>Pet name rules shared by the package naming dialog, purchases and nest breeding.</summary>
public static partial class PetNames
{
    [GeneratedRegex(@"^[\p{L}\p{N} .\-]+$")]
    private static partial Regex AllowedCharactersRegex();

    public static PetNameValidationType Validate(string? name, int minLength, int maxLength)
    {
        var trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length < minLength)
            return PetNameValidationType.TooShort;

        if (trimmed.Length > maxLength)
            return PetNameValidationType.TooLong;

        if (!AllowedCharactersRegex().IsMatch(trimmed))
            return PetNameValidationType.InvalidCharacters;

        return PetNameValidationType.Ok;
    }
}
