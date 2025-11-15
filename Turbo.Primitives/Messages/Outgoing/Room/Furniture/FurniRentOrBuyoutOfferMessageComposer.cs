using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record FurniRentOrBuyoutOfferMessageComposer : IComposer
{
    [Id(0)]
    public required bool IsWallItem { get; init; }

    [Id(1)]
    public required string FurniTypeName { get; init; }

    [Id(2)]
    public required bool Buyout { get; init; }

    [Id(3)]
    public required int PriceInCredits { get; init; }

    [Id(4)]
    public required int PriceInActivityPoints { get; init; }

    [Id(5)]
    public required int ActivityPointType { get; init; }
}
