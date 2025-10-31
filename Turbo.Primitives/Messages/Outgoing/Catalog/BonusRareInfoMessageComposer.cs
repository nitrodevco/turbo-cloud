using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

public record BonusRareInfoMessageComposer : IComposer
{
    public required string ProductType { get; init; }
    public required int ProductClassId { get; init; }
    public required int TotalCoinsForBonus { get; init; }
    public required int CoinsStillRequiredToBuy { get; init; }
}
