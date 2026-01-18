namespace Turbo.Primitives.Rooms.Enums.Wired;

public enum WiredFurniSourceType
{
    None = 0,
    TriggeredItem = 1,
    SelectedItems = 2,
    SnapshotItems = 3,
    SelectorItems = 4,
    SignalItems = 5,
    AllRoomItems = 6,
}

public static class WiredFurniSourceTypeExtensions
{
    public static WiredSourceType GetProtocolId(WiredFurniSourceType source)
    {
        return source switch
        {
            WiredFurniSourceType.TriggeredItem => WiredSourceType.TriggeredItem,
            WiredFurniSourceType.SelectedItems => WiredSourceType.SelectedItems,
            WiredFurniSourceType.SnapshotItems => WiredSourceType.SnapshotItems,
            WiredFurniSourceType.SelectorItems => WiredSourceType.SelectorItems,
            WiredFurniSourceType.SignalItems => WiredSourceType.SignalItems,
            WiredFurniSourceType.AllRoomItems => WiredSourceType.AllRoomItems,
            _ => WiredSourceType.__INTERNAL_SEPARATOR,
        };
    }

    public static WiredFurniSourceType FromProtocolId(WiredSourceType id) =>
        id switch
        {
            WiredSourceType.TriggeredItem => WiredFurniSourceType.TriggeredItem,
            WiredSourceType.SelectedItems => WiredFurniSourceType.SelectedItems,
            WiredSourceType.SnapshotItems => WiredFurniSourceType.SnapshotItems,
            WiredSourceType.SelectorItems => WiredFurniSourceType.SelectorItems,
            WiredSourceType.SignalItems => WiredFurniSourceType.SignalItems,
            WiredSourceType.AllRoomItems => WiredFurniSourceType.AllRoomItems,
            _ => WiredFurniSourceType.None,
        };
}
