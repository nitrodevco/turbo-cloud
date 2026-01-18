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
            WiredPlayerSourceType.TriggeredUser => WiredSourceType.TriggeredUser,
            WiredPlayerSourceType.ReachedUser => WiredSourceType.ReachedUser,
            WiredPlayerSourceType.ClickedUser => WiredSourceType.ClickedUser,
            WiredPlayerSourceType.BotByName => WiredSourceType.BotByName,
            WiredPlayerSourceType.UserByName => WiredSourceType.UserByName,
            WiredPlayerSourceType.SelectorUsers => WiredSourceType.SelectorUsers,
            WiredPlayerSourceType.SignalUsers => WiredSourceType.SignalUsers,
            WiredPlayerSourceType.AllRoomUsers => WiredSourceType.AllRoomUsers,
            _ => WiredSourceType.__INTERNAL_SEPARATOR,
        };
    }

    public static WiredPlayerSourceType FromProtocolId(WiredSourceType id) =>
        id switch
        {
            WiredSourceType.TriggeredUser => WiredPlayerSourceType.TriggeredUser,
            WiredSourceType.ReachedUser => WiredPlayerSourceType.ReachedUser,
            WiredSourceType.ClickedUser => WiredPlayerSourceType.ClickedUser,
            WiredSourceType.BotByName => WiredPlayerSourceType.BotByName,
            WiredSourceType.UserByName => WiredPlayerSourceType.UserByName,
            WiredSourceType.SelectorUsers => WiredPlayerSourceType.SelectorUsers,
            WiredSourceType.SignalUsers => WiredPlayerSourceType.SignalUsers,
            WiredSourceType.AllRoomUsers => WiredPlayerSourceType.AllRoomUsers,
            _ => WiredPlayerSourceType.None,
        };
}
