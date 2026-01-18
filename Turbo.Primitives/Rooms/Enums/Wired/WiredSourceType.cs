namespace Turbo.Primitives.Rooms.Enums.Wired;

public enum WiredSourceType
{
    TriggeredUser = 0,
    ReachedUser = 10,
    ClickedUser = 11,
    BotByName = 100,
    UserByName = 101,
    SelectorUsers = 200,
    SignalUsers = 201,
    AllRoomUsers = 900,
    __INTERNAL_SEPARATOR = -1,
    TriggeredItem = 0,
    SelectedItems = 100,
    SnapshotItems = 101,
    SelectorItems = 200,
    SignalItems = 201,
    AllRoomItems = 900,
}
