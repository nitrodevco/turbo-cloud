using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>Both sides of a wired trade, in the same item list a player-to-player trade sends, then whether the user can accept.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTradeItemsUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required PlayerId FirstPlayerId { get; init; }

    [Id(1)]
    public required ImmutableArray<FurnitureItemSnapshot> FirstItems { get; init; }

    [Id(2)]
    public required int FirstCredits { get; init; }

    [Id(3)]
    public required PlayerId SecondPlayerId { get; init; }

    [Id(4)]
    public required ImmutableArray<FurnitureItemSnapshot> SecondItems { get; init; }

    [Id(5)]
    public required int SecondCredits { get; init; }

    [Id(6)]
    public required bool CanAccept { get; init; }

    [Id(7)]
    public required int Extra { get; init; }
}
