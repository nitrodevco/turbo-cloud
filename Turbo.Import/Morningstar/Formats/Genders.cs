using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Import.Morningstar.Formats;

/// <summary>
/// A gender as Morningstar writes one: <c>M</c> or <c>F</c> in any case (a mannequin's too), or
/// the word in some older rows. Anything else is male.
/// </summary>
internal static class Genders
{
    public static AvatarGenderType Of(string? value)
    {
        var upper = value?.Trim().ToUpperInvariant() ?? string.Empty;

        return AvatarGenderTypeExtensions.FromLegacyString(upper == "FEMALE" ? "F" : upper);
    }
}
