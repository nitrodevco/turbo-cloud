using Turbo.Contracts.Enums.Rooms;

namespace Turbo.Furniture.Configuration;

public class FurnitureConfig
{
    public const string SECTION_NAME = "Turbo:Furniture";

    public float MinimumZValue { get; init; } = 0.001f;

    public int TileHeightMultiplier { get; init; } = 256;
    public RoomScaleType DefaultRoomScale { get; init; } = RoomScaleType.Normal;
    public int DefaultWallHeight { get; init; } = 0;
}
