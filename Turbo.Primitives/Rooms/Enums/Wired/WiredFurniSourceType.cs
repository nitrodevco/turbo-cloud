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

    /// <summary>The box's second pick list, for boxes that take two sets of furni.</summary>
    SecondaryItems = 7,

    /// <summary>
    /// Every furni the stack moves while it runs (sources.furni.901, "All furni moved by the Wired
    /// stack"); known only as the moves happen, so it selects nothing up front.
    /// </summary>
    MovedItems = 8,
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
            WiredFurniSourceType.SecondaryItems => WiredSourceType.SecondaryItems,
            WiredFurniSourceType.SelectorItems => WiredSourceType.SelectorItems,
            WiredFurniSourceType.SignalItems => WiredSourceType.SignalItems,
            WiredFurniSourceType.AllRoomItems => WiredSourceType.AllRoomItems,
            WiredFurniSourceType.MovedItems => WiredSourceType.MovedItems,
            _ => WiredSourceType.__INTERNAL_SEPARATOR,
        };
    }

    public static WiredFurniSourceType FromProtocolId(WiredSourceType id) =>
        id switch
        {
            WiredSourceType.TriggeredItem => WiredFurniSourceType.TriggeredItem,
            WiredSourceType.SelectedItems => WiredFurniSourceType.SelectedItems,
            WiredSourceType.SnapshotItems => WiredFurniSourceType.SnapshotItems,
            WiredSourceType.SecondaryItems => WiredFurniSourceType.SecondaryItems,
            WiredSourceType.SelectorItems => WiredFurniSourceType.SelectorItems,
            WiredSourceType.SignalItems => WiredFurniSourceType.SignalItems,
            WiredSourceType.AllRoomItems => WiredFurniSourceType.AllRoomItems,
            WiredSourceType.MovedItems => WiredFurniSourceType.MovedItems,
            _ => WiredFurniSourceType.None,
        };
}
