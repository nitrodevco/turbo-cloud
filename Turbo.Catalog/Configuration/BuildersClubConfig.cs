namespace Turbo.Catalog.Configuration;

/// <summary>
/// What the Builders Club itself keeps track of. How much a member may borrow is a property of
/// their subscription and lives on <c>PlayerConfig.Subscriptions</c>; whether a group room may
/// be built in is enforced by the room and lives on <c>RoomConfig</c>.
/// </summary>
public class BuildersClubConfig
{
    /// <summary>How often borrow counts are recounted from the rows that hold the truth.</summary>
    public int BorrowedCountRefreshMs { get; init; } = 300000;

    /// <summary>
    /// How often rooms holding borrowed furni are checked against their borrowers' memberships.
    /// A renewal is acted on at once, so this only has to catch memberships running out, which
    /// they do by the day.
    /// </summary>
    public int LapseSweepMs { get; init; } = 900000;
}
