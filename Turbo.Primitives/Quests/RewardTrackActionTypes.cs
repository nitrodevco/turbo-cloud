namespace Turbo.Primitives.Quests;

/// <summary>
/// What a reward track task counts. The client draws a task with the image
/// <c>reward_track_tasks_&lt;action type, lower case&gt;</c>; these are the thirty such images
/// the official client carries (JS build 87, habbo-window-manager-com), as many as the tasks of
/// Habbo's Introduction track (<c>reward_track.introduction.task.*</c> texts, "10 / 30" in the
/// official capture). The server counts the ones it hears of (see <c>RewardTrackFactListener</c>).
/// </summary>
public static class RewardTrackActionTypes
{
    public const string BUY_FROM_CATALOGUE = "buy_from_catalogue";
    public const string CHANGE_FIGURE = "change_figure";
    public const string CHANGE_MOTTO = "change_motto";
    public const string CHAT_WITH_SOMEONE = "chat_with_someone";
    public const string CREATE_ROOM = "create_room";
    public const string DANCE = "dance";
    public const string ENTER_OTHER_USERS_ROOM = "enter_other_users_room";
    public const string FIND_HAND_ITEM = "find_hand_item";
    public const string FOLLOW_FRIEND = "follow_friend";
    public const string FRIEND_FURNI_LOCKED = "friend_furni_locked";
    public const string GIVE_RESPECT = "give_respect";
    public const string MOVE_ITEM = "move_item";
    public const string PET_EAT = "pet_eat";
    public const string PET_LEVEL = "pet_level";
    public const string PET_RESPECT = "pet_respect";
    public const string PLACE_BUILDERS_CLUB_FURNI = "place_builders_club_furni";
    public const string PLACE_ITEM = "place_item";
    public const string PUBLISH_PICTURE = "publish_picture";
    public const string REPLENISH_RESPECT = "replenish_respect";
    public const string REQUEST_FRIEND = "request_friend";
    public const string ROTATE_ITEM = "rotate_item";
    public const string SEND_MESSENGER_INVITE = "send_messenger_invite";
    public const string SEND_MESSENGER_MESSAGE = "send_messenger_message";
    public const string SET_RELATIONSHIP_STATUS = "set_relationship_status";
    public const string SWIM = "swim";
    public const string SWITCH_ITEM_STATE = "switch_item_state";
    public const string TELEPORT = "teleport";
    public const string USE_HABBICON = "use_habbicon";
    public const string WAVE = "wave";
    public const string WEAR_BADGE = "wear_badge";
}
