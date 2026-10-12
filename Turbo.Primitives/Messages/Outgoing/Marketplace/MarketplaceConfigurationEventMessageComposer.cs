using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Marketplace;

[GenerateSerializer, Immutable]
public sealed record MarketplaceConfigurationEventMessageComposer : IComposer
{
    [Id(0)]
    public required bool Enabled { get; init; }

    [Id(1)]
    public required int Commission { get; init; }

    [Id(2)]
    public required int TokenBatchPrice { get; init; }

    [Id(3)]
    public required int TokenBatchSize { get; init; }

    [Id(4)]
    public required int OfferMinPrice { get; init; }

    [Id(5)]
    public required int OfferMaxPrice { get; init; }

    [Id(6)]
    public required int ExpirationHours { get; init; }

    [Id(7)]
    public required int AveragePricePeriod { get; init; }

    [Id(8)]
    public required int SellingFeePercentage { get; init; }

    [Id(9)]
    public required int RevenueLimit { get; init; }

    [Id(10)]
    public required int HalfTaxLimit { get; init; }
}
