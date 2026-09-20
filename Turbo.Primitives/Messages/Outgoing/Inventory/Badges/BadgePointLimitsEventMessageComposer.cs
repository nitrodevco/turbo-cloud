using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Badges;

[GenerateSerializer, Immutable]
public sealed record BadgePointLimitsEventMessageComposer : IComposer
{
    [Id(0)]
    public required List<BadgePointLimitGroupSnapshot> LimitsByBadgeCodePrefix { get; init; }
}
