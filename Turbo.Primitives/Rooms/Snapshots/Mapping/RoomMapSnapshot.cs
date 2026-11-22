using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots.Mapping;

[GenerateSerializer, Immutable]
public sealed record RoomMapSnapshot
{
    [Id(0)]
    public required string ModelName { get; init; }

    [Id(1)]
    public required string ModelData { get; init; }

    [Id(2)]
    public required int Width { get; init; }

    [Id(3)]
    public required int Height { get; init; }

    [Id(4)]
    public required int Size { get; init; }

    /// <summary>
    /// The tile people come in on (<c>RoomMapModule.EntryX</c>): the model's door, or the open
    /// tile nearest it when the model's door is no tile of the model.
    /// </summary>
    [Id(5)]
    public required int DoorX { get; init; }

    /// <summary>The row of the tile people come in on; see <see cref="DoorX"/>.</summary>
    [Id(6)]
    public required int DoorY { get; init; }

    [Id(7)]
    public required Rotation DoorRotation { get; init; }

    [Id(8)]
    public required short[] TileEncodedHeights { get; init; }
}
