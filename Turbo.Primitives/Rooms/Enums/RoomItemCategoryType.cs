namespace Turbo.Primitives.Rooms.Enums;

/// <summary>
/// How the Flash client names a furni's kind in the pickup packets
/// (<c>PickupObjectMessageComposer.getMessageArray</c>, <c>ObjectRemoveConfirmMessageParser</c>):
/// a wall item (room object category 20) as 1, a floor item (10) as 2.
/// </summary>
public enum RoomItemCategoryType
{
    Wall = 1,
    Floor = 2,
}
