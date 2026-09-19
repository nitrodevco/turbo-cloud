namespace Turbo.Primitives.Badges;

/// <summary>
/// Rank values the client gives a meaning to. It shows a badges rank only when it is zero or
/// more (<c>InfoStandUserView.badgesRank</c>, <c>ExtendedProfileWindowCtrl</c>), so a player
/// with no badges, and anything that is not a player, is sent <see cref="NONE"/>.
/// </summary>
public static class BadgeRanks
{
    public const int NONE = -1;
}
