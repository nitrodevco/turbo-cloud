using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Primitives.Furniture;

/// <summary>
/// Plain furniture's logic: what a definition runs until it is given a logic of its own, and what
/// the room falls back to for a logic it has not registered (<c>RoomObjectLogicProvider</c>).
/// </summary>
public static class DefaultFurnitureLogic
{
    public const string FLOOR = "default_floor";
    public const string WALL = "default_wall";

    /// <summary>Plain furniture's logic for a kind of furni.</summary>
    public static string For(ProductType type) => type == ProductType.Wall ? WALL : FLOOR;

    /// <summary>Whether a logic is plain furniture's, floor or wall.</summary>
    public static bool IsDefault(string logic) => logic is FLOOR or WALL;
}
