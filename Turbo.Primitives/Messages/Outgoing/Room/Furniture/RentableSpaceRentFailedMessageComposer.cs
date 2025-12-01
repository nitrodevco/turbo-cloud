using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record RentableSpaceRentFailedMessageComposer : IComposer
{
    [Id(0)]
    public required RentableSpaceRentFailedType Reason { get; init; }
}
