using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record DiceValueMessageComposer : IComposer
{
    [Id(0)]
    public required int FurniId { get; init; }

    [Id(1)]
    public required int Value { get; init; }
}
