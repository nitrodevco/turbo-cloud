using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Configuration;

public class RoomConfig
{
    public const string SECTION_NAME = "Turbo:Rooms";

    public double MaxStackHeight { get; init; } = 40.0;
    public RoomScaleType DefaultRoomScale { get; init; } = RoomScaleType.Normal;
    public int DefaultWallHeight { get; init; } = 0;
    public double MaxStepHeight { get; init; } = 2.0;
    public bool PlaceItemsOnAvatars { get; init; } = true;
    public bool EnableDiagonalChecking { get; init; } = true;

    public int RoomCheckMs { get; init; } = 300000;
    public int RoomDeactivationDelayMs { get; init; } = 1800000;
    public int RoomTickMs { get; init; } = 50;
    public int AvatarTickMs { get; init; } = 500;
    public int RollerTickMs { get; init; } = 2000;
    public int WiredTickMs { get; init; } = 50;
    public int DirtyItemsTickMs { get; init; } = 2000;
    public int MaxDirtyItemsPerFlush { get; init; } = 100;
    public int MaxTileHeightsPerFlush { get; init; } = 200;
    public int MaxPathNodes { get; init; } = 4096;

    public int WiredMaxDepth { get; init; } = 20;
    public int WiredMaxScheduledPerTick { get; init; } = 64;
    public int WiredMaxEventsPerTick { get; init; } = 64;
}
