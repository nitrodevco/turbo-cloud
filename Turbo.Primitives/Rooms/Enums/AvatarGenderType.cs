using System;

namespace Turbo.Primitives.Rooms.Enums;

public enum AvatarGenderType
{
    Male = 0,
    Female = 1,
}

public static class AvatarGenderTypeExtensions
{
    public static string ToLegacyString(this AvatarGenderType gender) =>
        gender switch
        {
            AvatarGenderType.Male => "M",
            AvatarGenderType.Female => "F",
            _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
        };

    public static AvatarGenderType FromLegacyString(string genderString) =>
        genderString switch
        {
            "M" => AvatarGenderType.Male,
            "Male" => AvatarGenderType.Male,
            "F" => AvatarGenderType.Female,
            "Female" => AvatarGenderType.Female,
            _ => AvatarGenderType.Male,
        };
}
