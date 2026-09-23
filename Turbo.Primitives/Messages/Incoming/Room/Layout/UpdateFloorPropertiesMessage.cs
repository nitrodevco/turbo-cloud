using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Layout;

/// <summary>
/// A floor plan saved from the editor. Every number here came from a client and is carried raw:
/// the door direction and the two thicknesses only become their enums once the room has checked
/// that they name something.
/// <para>
/// The editor sends the tail only when it has one. A save with nothing but the heightmap leaves
/// the door and the visual settings exactly as they were.
/// </para>
/// </summary>
public record UpdateFloorPropertiesMessage : IMessageEvent
{
    public required string ModelData { get; init; }

    /// <summary>True when the client sent the door and the wall and floor settings as well.</summary>
    public bool HasProperties { get; init; }

    public int DoorX { get; init; } = -1;
    public int DoorY { get; init; } = -1;
    public int DoorRotation { get; init; } = -1;
    public int WallThickness { get; init; }
    public int FloorThickness { get; init; }

    /// <summary>The height every wall is drawn at, or -1 when the room decides for itself.</summary>
    public int FixedWallsHeight { get; init; } = -1;
}
