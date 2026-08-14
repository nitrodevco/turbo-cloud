using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record RoomVisualizationSettingsMessageComposer : IComposer
{
    [Id(0)]
    public required bool WallsHidden { get; init; }

    [Id(1)]
    public required RoomThicknessType WallThickness { get; init; }

    [Id(2)]
    public required RoomThicknessType FloorThickness { get; init; }
}
