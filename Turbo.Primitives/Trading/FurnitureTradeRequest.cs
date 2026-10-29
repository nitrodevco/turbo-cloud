using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Trading;

[GenerateSerializer, Immutable]
public sealed record FurnitureTradeRequest
{
    [Id(0)]
    public required PlayerId InitiatorId { get; init; }

    [Id(1)]
    public required PlayerId PartnerId { get; init; }

    [Id(2)]
    public required ImmutableArray<FurnitureItemSnapshot> InitiatorItems { get; init; }

    [Id(3)]
    public required ImmutableArray<FurnitureItemSnapshot> PartnerItems { get; init; }
}
