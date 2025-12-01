using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record RentableSpaceStatusMessageComposer : IComposer
{
    [Id(0)]
    public required bool Rented { get; init; }

    [Id(1)]
    public required RentableSpaceRentFailedType CanRentErrorCode { get; init; }

    [Id(2)]
    public required int RenterId { get; init; }

    [Id(3)]
    public required string RenterName { get; init; }

    [Id(4)]
    public required int TimeRemaining { get; init; }

    [Id(5)]
    public required int Price { get; init; }
}
