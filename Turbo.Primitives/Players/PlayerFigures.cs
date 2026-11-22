using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Players;

/// <summary>
/// A player's figure as stored: the longest one the <c>players.figure</c> column holds, and the
/// one a player wears when none is given, by gender (clothing and colours anyone may wear, so a
/// player outside the club keeps it as it is).
/// </summary>
public static class PlayerFigures
{
    public const int MAX_LENGTH = 279;

    public const string MALE = "hr-115-42.hd-195-19.ch-3030-82.lg-275-1408.fa-1201.ca-1804-64";
    public const string FEMALE = "hr-515-42.hd-600-1.ch-635-82.lg-716-66-66.sh-735-68";

    /// <summary>The figure a player of this gender wears when none is given.</summary>
    public static string DefaultFor(AvatarGenderType gender) =>
        gender == AvatarGenderType.Female ? FEMALE : MALE;
}
