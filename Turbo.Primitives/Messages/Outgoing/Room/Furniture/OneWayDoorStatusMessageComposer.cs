using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record OneWayDoorStatusMessageComposer : IComposer
{
    [Id(0)]
    public required int FurniId { get; init; }

    [Id(1)]
    public required bool Status { get; init; }
}
