namespace Turbo.Primitives.Rooms.Enums.Wired;

public enum WiredPlayerSourceType
{
    None = 0,
    TriggeredUser = 1,
    ReachedUser = 2,
    ClickedUser = 3,
    BotByName = 4,
    UserByName = 5,
    SelectorUsers = 6,
    SignalUsers = 7,
    AllRoomUsers = 8,
}

public static class WiredPlayerSourceTypeExtensions
{
    public static WiredSourceType GetProtocolId(WiredPlayerSourceType source)
    {
        return source switch
        {
            WiredPlayerSourceType.TriggeredUser => WiredSourceType.TRIGGERED_USER,
            WiredPlayerSourceType.ReachedUser => WiredSourceType.REACHED_USER,
            WiredPlayerSourceType.ClickedUser => WiredSourceType.CLICKED_USER,
            WiredPlayerSourceType.BotByName => WiredSourceType.BOT_BY_NAME,
            WiredPlayerSourceType.UserByName => WiredSourceType.USER_BY_NAME,
            WiredPlayerSourceType.SignalUsers => WiredSourceType.SIGNAL_USERS,
            WiredPlayerSourceType.AllRoomUsers => WiredSourceType.ALL_ROOM_USERS,
            _ => WiredSourceType.__INTERNAL_SEPARATOR,
        };
    }

    public static WiredPlayerSourceType FromProtocolId(WiredSourceType id) =>
        id switch
        {
            WiredSourceType.TRIGGERED_USER => WiredPlayerSourceType.TriggeredUser,
            WiredSourceType.REACHED_USER => WiredPlayerSourceType.ReachedUser,
            WiredSourceType.CLICKED_USER => WiredPlayerSourceType.ClickedUser,
            WiredSourceType.BOT_BY_NAME => WiredPlayerSourceType.BotByName,
            WiredSourceType.USER_BY_NAME => WiredPlayerSourceType.UserByName,
            WiredSourceType.SELECTOR_USERS => WiredPlayerSourceType.SelectorUsers,
            WiredSourceType.SIGNAL_USERS => WiredPlayerSourceType.SignalUsers,
            WiredSourceType.ALL_ROOM_USERS => WiredPlayerSourceType.AllRoomUsers,
            _ => WiredPlayerSourceType.None,
        };
}
