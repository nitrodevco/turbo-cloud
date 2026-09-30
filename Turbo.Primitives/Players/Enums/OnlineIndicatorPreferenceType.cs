namespace Turbo.Primitives.Players.Enums;

/// <summary>
/// Whose coming online the player is told about (the "friend online" notification), as
/// <c>OtherSettingsView</c>'s drop menu offers it and <c>FriendCategories.shouldNotifyFriendOnline</c>
/// reads it. The client applies it; the server only keeps it.
/// </summary>
public enum OnlineIndicatorPreferenceType
{
    Everyone = 0,
    RelationshipStatus = 1,
    Nobody = 2,
}
