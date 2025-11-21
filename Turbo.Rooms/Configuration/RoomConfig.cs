using Turbo.Contracts.Enums.Rooms;

namespace Turbo.Rooms.Configuration;

public class RoomConfig
{
    public const string SECTION_NAME = "Turbo:Rooms";

    public int RoomDeactivationTimeoutSeconds { get; init; } = 10;
    public int TileHeightMultiplier { get; init; } = 256;
    public int DirtyItemsFlushIntervalMilliseconds { get; init; } = 5000;
    public int DirtyTilesFlushIntervalMilliseconds { get; init; } = 10;
    public double MaxStackHeight { get; init; } = 40.0;
    public RoomScaleType DefaultRoomScale { get; init; } = RoomScaleType.Normal;
    public int DefaultWallHeight { get; init; } = 0;
}
