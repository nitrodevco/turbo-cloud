using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record BonusRareInfoMessageComposer : IComposer
{
    [Id(0)]
    public required string ProductType { get; init; }

    [Id(1)]
    public required int ProductClassId { get; init; }

    [Id(2)]
    public required int TotalCoinsForBonus { get; init; }

    [Id(3)]
    public required int CoinsStillRequiredToBuy { get; init; }
}
