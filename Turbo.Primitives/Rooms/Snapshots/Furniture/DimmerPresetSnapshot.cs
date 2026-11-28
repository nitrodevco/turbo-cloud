using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Furniture;

/// <summary>
/// One of a dimmer's presets. The dimmer keeps its three as a list under <see cref="SECTION"/> in
/// its item's extra data.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record DimmerPresetSnapshot
{
    public const string SECTION = "dimmer";

    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required int Type { get; init; }

    [Id(2)]
    public required string Color { get; init; }

    [Id(3)]
    public required int Brightness { get; init; }

    /// <summary>
    /// Whether furni keep their own lights under this preset (a client with
    /// <c>room_dimmer_lights</c> sets it). False in every preset stored before it existed.
    /// </summary>
    [Id(4)]
    public bool KeepFurnitureLights { get; init; }
}
