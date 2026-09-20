namespace Turbo.Primitives.Catalog;

/// <summary>
/// The notification types the Builders Club sends. Each one has to exist in the hotel client's
/// <c>ExternalVariables.json</c> as <c>notification.&lt;type&gt;</c>, or the client draws nothing
/// at all; these four are the ones the stock hotel data configures.
/// </summary>
public static class BuildersClubNotifications
{
    /// <summary>The room has gone off the navigator because a borrower let their membership lapse.</summary>
    public const string ROOM_LOCKED = "builders_club.room_locked";

    /// <summary>The room is back on the navigator.</summary>
    public const string ROOM_UNLOCKED = "builders_club.room_unlocked";

    /// <summary>Shown to somebody turned away from a room that is hidden.</summary>
    public const string VISIT_DENIED_FOR_VISITOR = "builders_club.visit_denied_for_visitor";

    /// <summary>
    /// The owner's half of the same. Nothing sends it: it would arrive once per turned-away
    /// visitor, which anyone holding the room's link could set off at will. It waits for a
    /// feature that reports who tried, at a pace the owner chooses.
    /// </summary>
    public const string VISIT_DENIED_FOR_OWNER = "builders_club.visit_denied_for_owner";
}
