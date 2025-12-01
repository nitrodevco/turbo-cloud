using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record PresentOpenedMessageComposer : IComposer
{
    [Id(0)]
    public required string ItemType { get; init; }

    [Id(1)]
    public required int ClassId { get; init; }

    [Id(2)]
    public required ProductType ProductCode { get; init; }

    [Id(3)]
    public required int PlacedItemId { get; init; }

    [Id(4)]
    public required ProductType PlacedItemType { get; init; }

    [Id(5)]
    public required bool PlacedInRoom { get; init; }

    [Id(6)]
    public required string PetFigureString { get; init; }
}
