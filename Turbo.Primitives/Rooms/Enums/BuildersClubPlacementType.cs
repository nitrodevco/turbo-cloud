namespace Turbo.Primitives.Rooms.Enums;

/// <summary>
/// Which of the two Builders Club placement requests a warning is about. The client switches on
/// it to decide whether to read a tile and direction back or a wall location, and to rebuild the
/// request it will re-send (<c>RoomMessageHandler.onBCPlacementWarning</c>).
/// </summary>
public enum BuildersClubPlacementType
{
    FloorItem = 0,
    WallItem = 1,
}
