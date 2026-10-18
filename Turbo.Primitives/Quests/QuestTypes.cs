namespace Turbo.Primitives.Quests;

/// <summary>
/// What counts towards a quest, as the quest's type. The AS3 client knows types in this form
/// (QuestDetails: PLACE_ITEM, PLACE_FLOOR, PLACE_WALLPAPER, PET_DRINK, PET_EAT); WEAR_BADGE, the
/// badge campaigns' "Wear the ... badge", is inferred from their texts, and FRIEND_REQUEST, the
/// daily "Send three friend requests" quests (quests.daily.FRIEND1/2), from the client's
/// FriendRequestQuestComplete.
/// </summary>
public static class QuestTypes
{
    public const string WEAR_BADGE = "WEAR_BADGE";
    public const string PET_EAT = "PET_EAT";
    public const string FRIEND_REQUEST = "FRIEND_REQUEST";
}
