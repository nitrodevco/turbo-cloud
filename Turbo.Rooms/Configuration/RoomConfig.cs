using Turbo.Contracts.Enums.Rooms;

namespace Turbo.Rooms.Configuration;

public class RoomConfig
{
    public const string SECTION_NAME = "Turbo:Rooms";

    public int RoomDeactivationTimeoutSeconds { get; init; } = 10;
    public int TileHeightMultiplier { get; init; } = 256;
    public int DirtyItemsFlushIntervalSeconds { get; init; } = 5;
    public int DirtyTilesFlushIntervalSeconds { get; init; } = 5;
    public RoomScaleType DefaultRoomScale { get; init; } = RoomScaleType.Normal;
    public int DefaultWallHeight { get; init; } = 0;
}
