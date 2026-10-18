using System.Collections.Immutable;

namespace Turbo.Primitives.Quests;

/// <summary>
/// What a reward track task counts: the thirty action types of Habbo's Introduction track, each
/// with its <c>reward_track_tasks_&lt;action type, lower case&gt;</c> image in the client, and the
/// five more of its Habboween 2026 track (RewardTracks, 2026-10-18 official client capture), which
/// the client draws by task (<c>reward_track_tasks_habboween2026_&lt;task&gt;</c>, AS3 1745
/// RewardTrackController.getTaskIconAssetName). The server counts every one; what each counts is
/// read from its tasks' hotel texts (<c>reward_track.&lt;track&gt;.task.&lt;id&gt;.desc</c>).
/// </summary>
public static class RewardTrackActionTypes
{
    public const string BUY_FROM_CATALOGUE = "buy_from_catalogue";
    public const string CHANGE_FIGURE = "change_figure";
    public const string CHANGE_MOTTO = "change_motto";
    public const string CHAT_WITH_SOMEONE = "chat_with_someone";
    public const string CREATE_ROOM = "create_room";

    /// <summary>
    /// Counted only by the wired Progress Reward Track action (Habbo's Habboween campaign tasks):
    /// no player action is this type.
    /// </summary>
    public const string CUSTOM_WIRED_ACTION = "custom_wired_action";
    public const string DANCE = "dance";
    public const string ENTER_OTHER_USERS_ROOM = "enter_other_users_room";
    public const string FIND_HAND_ITEM = "find_hand_item";
    public const string FOLLOW_FRIEND = "follow_friend";
    public const string FRIEND_FURNI_LOCKED = "friend_furni_locked";
    public const string GIVE_RESPECT = "give_respect";
    public const string MOVE_ITEM = "move_item";
    public const string PET_EAT = "pet_eat";
    public const string PET_LEVEL = "pet_level";

    /// <summary>A player's own pet put down in a room someone else owns ("Take your pet to visit a friend").</summary>
    public const string PET_OTHER_USERS_ROOM = "pet_other_users_room";
    public const string PET_RESPECT = "pet_respect";
    public const string PLACE_BUILDERS_CLUB_FURNI = "place_builders_club_furni";
    public const string PLACE_ITEM = "place_item";

    /// <summary>A monsterplant seed planted ("Plant a monsterplant seed").</summary>
    public const string PLANT_MONSTER_PLANT = "plant_monster_plant";

    /// <summary>A monsterplant bred: each of its two owners ("Breed a monsterplant").</summary>
    public const string BREED_MONSTER_PLANT = "breed_monster_plant";

    /// <summary>A monsterplant treated with the plant menu's Treat ("Treat a monsterplant").</summary>
    public const string TREAT_MONSTER_PLANT = "treat_monster_plant";
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

    /// <summary>Every type the server counts, in lower case as above.</summary>
    public static readonly ImmutableArray<string> ALL =
    [
        BUY_FROM_CATALOGUE,
        CHANGE_FIGURE,
        CHANGE_MOTTO,
        CHAT_WITH_SOMEONE,
        CREATE_ROOM,
        CUSTOM_WIRED_ACTION,
        DANCE,
        ENTER_OTHER_USERS_ROOM,
        FIND_HAND_ITEM,
        FOLLOW_FRIEND,
        FRIEND_FURNI_LOCKED,
        GIVE_RESPECT,
        MOVE_ITEM,
        PET_EAT,
        PET_LEVEL,
        PET_OTHER_USERS_ROOM,
        PET_RESPECT,
        PLACE_BUILDERS_CLUB_FURNI,
        PLACE_ITEM,
        PLANT_MONSTER_PLANT,
        BREED_MONSTER_PLANT,
        TREAT_MONSTER_PLANT,
        PUBLISH_PICTURE,
        REPLENISH_RESPECT,
        REQUEST_FRIEND,
        ROTATE_ITEM,
        SEND_MESSENGER_INVITE,
        SEND_MESSENGER_MESSAGE,
        SET_RELATIONSHIP_STATUS,
        SWIM,
        SWITCH_ITEM_STATE,
        TELEPORT,
        USE_HABBICON,
        WAVE,
        WEAR_BADGE,
    ];
}
