using System;

namespace Turbo.Primitives.Rooms.Enums;

public enum AvatarGenderEnum
{
    Male = 0,
    Female = 1,
}

public static class AvatarGenderEnumExtensions
{
    public static string ToLegacyString(this AvatarGenderEnum gender) =>
        gender switch
        {
            AvatarGenderEnum.Male => "M",
            AvatarGenderEnum.Female => "F",
            _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
        };

    public static AvatarGenderEnum? FromLegacyString(string genderString) =>
        genderString switch
        {
            "M" => AvatarGenderEnum.Male,
            "Male" => AvatarGenderEnum.Male,
            "F" => AvatarGenderEnum.Female,
            "Female" => AvatarGenderEnum.Female,
            _ => null,
        };
}
