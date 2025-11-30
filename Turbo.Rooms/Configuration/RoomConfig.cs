using Turbo.Contracts.Enums.Rooms;

namespace Turbo.Rooms.Configuration;

public class RoomConfig
{
    public const string SECTION_NAME = "Turbo:Rooms";

    public int RoomCheckIntervalMilliseconds { get; init; } = 300000;
    public int RoomDeactivationDelayMilliseconds { get; init; } = 1800000;
    public int DirtyItemsFlushIntervalMilliseconds { get; init; } = 5000;
    public int DirtyTilesFlushIntervalMilliseconds { get; init; } = 10;
    public int DirtyAvatarsFlushIntervalMilliseconds { get; init; } = 500;
    public double MaxStackHeight { get; init; } = 40.0;
    public RoomScaleType DefaultRoomScale { get; init; } = RoomScaleType.Normal;
    public int DefaultWallHeight { get; init; } = 0;
    public double MaxStepHeight { get; init; } = 2.0;
    public bool AllowDiagonalMovement { get; init; } = true;
}
