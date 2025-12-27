namespace Turbo.Primitives.Rooms.Enums.Wired;

public enum WiredSourceType
{
    TRIGGERED_USER = 0,
    REACHED_USER = 10,
    CLICKED_USER = 11,
    BOT_BY_NAME = 100,
    USER_BY_NAME = 101,
    SELECTOR_USERS = 200,
    SIGNAL_USERS = 201,
    ALL_ROOM_USERS = 900,
    __INTERNAL_SEPARATOR = -1,
    TRIGGERED_ITEM = 0,
    SELECTED_ITEMS = 100,
    SNAPSHOT_ITEMS = 101,
    SELECTOR_ITEMS = 200,
    SIGNAL_ITEMS = 201,
    ALL_ROOM_ITEMS = 900,
}
