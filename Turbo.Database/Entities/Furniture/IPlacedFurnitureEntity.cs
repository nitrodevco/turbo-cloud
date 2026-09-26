using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Database.Entities.Furniture;

/// <summary>
/// Where a furni row stands and what data it carries: the columns a hotel furni
/// (<see cref="FurnitureEntity"/>) and a borrowed Builders Club one
/// (<see cref="BuildersClubFurnitureEntity"/>) share, so a room writes them back one way.
/// </summary>
public interface IPlacedFurnitureEntity
{
    public int X { get; set; }
    public int Y { get; set; }
    public double Z { get; set; }
    public Rotation Rotation { get; set; }
    public int WallOffset { get; set; }
    public string? ExtraData { get; set; }
}
