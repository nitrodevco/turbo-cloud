using Orleans;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Badges;

[GenerateSerializer, Immutable]
public sealed record BadgeInfoMessageComposer : IComposer
{
    /// <summary>The asking player's badge row id, 0 when they do not own the badge.</summary>
    [Id(0)]
    public required int BadgeId { get; init; }

    [Id(1)]
    public required BadgeInfoSnapshot Info { get; init; }
}
