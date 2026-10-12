namespace Turbo.Primitives.Moderation.Enums;

/// <summary>
/// What a row of <c>moderation_actions</c> did. Stored as its number, so a value keeps its
/// number for good: new ones go on the end of their group's range.
/// </summary>
public enum ModerationActionType : short
{
    // The hotel: a player's standing everywhere.

    /// <summary>Kept out of the hotel, until the row's expiry or for good.</summary>
    Ban = 1,

    /// <summary>A ban lifted before its time.</summary>
    Unban = 2,

    /// <summary>Unable to talk anywhere, until the row's expiry.</summary>
    Silence = 3,

    /// <summary>A silence lifted before its time.</summary>
    Unsilence = 4,

    /// <summary>Unable to trade, until the row's expiry.</summary>
    TradeLock = 5,

    /// <summary>A trade lock lifted before its time.</summary>
    TradeUnlock = 6,

    /// <summary>Sent out of the hotel, free to come back.</summary>
    Disconnect = 7,

    /// <summary>Sent a moderator's warning; the detail is the message.</summary>
    Warn = 8,

    /// <summary>Sent a moderator's alert; the detail is the message.</summary>
    Alert = 9,

    // A room: a player's standing in one room, or the room itself.

    /// <summary>Sent out of the room.</summary>
    RoomKick = 100,

    /// <summary>Kept out of the room, until the row's expiry or for good.</summary>
    RoomBan = 101,

    /// <summary>A room ban lifted.</summary>
    RoomUnban = 102,

    /// <summary>Unable to talk in the room, until the row's expiry.</summary>
    RoomMute = 103,

    /// <summary>A room mute lifted.</summary>
    RoomUnmute = 104,

    /// <summary>Nobody without rights may talk in the room.</summary>
    RoomMuteAll = 105,

    /// <summary>The room may talk again.</summary>
    RoomUnmuteAll = 106,

    /// <summary>Everyone sent out of the room; the detail is how many.</summary>
    RoomKickAll = 107,

    /// <summary>Everyone sent out and the room closed until someone enters again.</summary>
    RoomUnload = 108,

    /// <summary>
    /// Muted by the room's flood protection for talking too fast, until the row's expiry. The
    /// hotel did it, so there is no actor.
    /// </summary>
    RoomFloodMute = 109,

    /// <summary>A player's rights in the room taken away.</summary>
    RoomRightsRemoved = 110,

    /// <summary>Everyone's rights in the room taken away.</summary>
    RoomRightsRemovedAll = 111,

    /// <summary>The room's settings changed by staff who do not own it; the detail is what changed.</summary>
    RoomSettingsChanged = 112,

    // What staff hand out and take away.

    /// <summary>A badge given; the detail is its code.</summary>
    BadgeGiven = 200,

    /// <summary>A badge taken away; the detail is its code.</summary>
    BadgeTaken = 201,

    /// <summary>Furni given; the detail is what and how many.</summary>
    ItemGiven = 202,

    /// <summary>A present sent from the hotel; the detail is what was in it.</summary>
    GiftSent = 203,

    // A group and its forum.

    /// <summary>A forum thread hidden from the group's members.</summary>
    ForumThreadHidden = 300,

    /// <summary>A hidden forum thread shown again.</summary>
    ForumThreadRestored = 301,

    /// <summary>A forum post hidden from the group's members.</summary>
    ForumPostHidden = 302,

    /// <summary>A hidden forum post shown again.</summary>
    ForumPostRestored = 303,

    /// <summary>A group renamed by staff; the detail is the new name.</summary>
    GroupRenamed = 310,

    /// <summary>A group's badge reset by staff to the default.</summary>
    GroupBadgeReset = 311,

    /// <summary>A member removed from a group by staff.</summary>
    GroupMemberRemoved = 312,

    /// <summary>A group deleted by staff; the detail is its name.</summary>
    GroupDeleted = 313,
}
