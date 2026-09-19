using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Trading;

/// <summary>Both offers, sent to both parties whenever either changes.</summary>
[GenerateSerializer, Immutable]
public sealed record TradingItemListEventMessageComposer : IComposer
{
    [Id(0)]
    public required PlayerId FirstPlayerId { get; init; }

    [Id(1)]
    public required ImmutableArray<FurnitureItemSnapshot> FirstItems { get; init; }

    /// <summary>Credits offered; this client has no way to offer any, so the room sends none.</summary>
    [Id(2)]
    public required int FirstCredits { get; init; }

    [Id(3)]
    public required PlayerId SecondPlayerId { get; init; }

    [Id(4)]
    public required ImmutableArray<FurnitureItemSnapshot> SecondItems { get; init; }

    [Id(5)]
    public required int SecondCredits { get; init; }
}
