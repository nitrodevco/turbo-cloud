using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Wired;

/// <summary>Where a floor furni stands, or will stand once the moves planned before it are made.</summary>
public readonly record struct WiredFurniPlacement(int X, int Y, Altitude Z, Rotation Rotation)
{
    public static WiredFurniPlacement Of(IRoomFloorItem item) =>
        new(item.X, item.Y, item.Z, item.Rotation);
}
