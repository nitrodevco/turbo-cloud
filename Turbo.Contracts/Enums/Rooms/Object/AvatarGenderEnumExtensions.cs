using System;

namespace Turbo.Contracts.Enums.Rooms.Object;

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
