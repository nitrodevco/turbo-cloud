using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Layout;

[GenerateSerializer, Immutable]
public sealed record RoomEntryTileMessageComposer : IComposer
{
    [Id(0)]
    public required int X { get; init; }

    [Id(1)]
    public required int Y { get; init; }

    [Id(2)]
    public required Rotation Rotation { get; init; }
}
