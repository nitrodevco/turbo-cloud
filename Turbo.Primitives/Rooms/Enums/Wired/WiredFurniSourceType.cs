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
            WiredFurniSourceType.TriggeredItem => WiredSourceType.TRIGGERED_ITEM,
            WiredFurniSourceType.SelectedItems => WiredSourceType.SELECTED_ITEMS,
            WiredFurniSourceType.SnapshotItems => WiredSourceType.SNAPSHOT_ITEMS,
            WiredFurniSourceType.SelectorItems => WiredSourceType.SELECTOR_ITEMS,
            WiredFurniSourceType.SignalItems => WiredSourceType.SIGNAL_ITEMS,
            WiredFurniSourceType.AllRoomItems => WiredSourceType.ALL_ROOM_ITEMS,
            _ => WiredSourceType.__INTERNAL_SEPARATOR,
        };
    }

    public static WiredFurniSourceType FromProtocolId(WiredSourceType id) =>
        id switch
        {
            WiredSourceType.TRIGGERED_ITEM => WiredFurniSourceType.TriggeredItem,
            WiredSourceType.SELECTED_ITEMS => WiredFurniSourceType.SelectedItems,
            WiredSourceType.SNAPSHOT_ITEMS => WiredFurniSourceType.SnapshotItems,
            WiredSourceType.SELECTOR_ITEMS => WiredFurniSourceType.SelectorItems,
            WiredSourceType.SIGNAL_ITEMS => WiredFurniSourceType.SignalItems,
            WiredSourceType.ALL_ROOM_ITEMS => WiredFurniSourceType.AllRoomItems,
            _ => WiredFurniSourceType.None,
        };
}
