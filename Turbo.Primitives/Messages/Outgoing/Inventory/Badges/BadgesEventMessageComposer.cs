using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Badges;

/// <summary>One fragment of the player's badges. Which ones are worn is not part of it; that arrives as the player's own <c>HabboUserBadges</c>.</summary>
[GenerateSerializer, Immutable]
public sealed record BadgesEventMessageComposer : IComposer
{
    [Id(0)]
    public required int TotalFragments { get; init; }

    [Id(1)]
    public required int FragmentNo { get; init; }

    [Id(2)]
    public required ImmutableArray<PlayerBadgeSnapshot> Badges { get; init; }
}
