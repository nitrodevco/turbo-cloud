using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Configuration;

public class RoomConfig
{
    public const string SECTION_NAME = "Turbo:Rooms";

    public int RoomCheckIntervalMilliseconds { get; init; } = 300000;
    public int RoomDeactivationDelayMilliseconds { get; init; } = 1800000;
    public int RoomTickMilliseconds { get; init; } = 500;
    public int RoomRollerTickCount { get; init; } = 4;
    public int DirtyItemsFlushIntervalMilliseconds { get; init; } = 5250;
    public int DirtyTilesFlushIntervalMilliseconds { get; init; } = 50;
    public double MaxStackHeight { get; init; } = 40.0;
    public RoomScaleType DefaultRoomScale { get; init; } = RoomScaleType.Normal;
    public int DefaultWallHeight { get; init; } = 0;
    public double MaxStepHeight { get; init; } = 2.0;
    public bool AllowDiagonalMovement { get; init; } = true;
    public bool PlaceItemsOnAvatars { get; init; } = false;
    public bool EnableDiagonalChecking { get; init; } = true;
}
