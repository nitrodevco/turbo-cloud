using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Badges;

[GenerateSerializer, Immutable]
public sealed record BadgePointLimitsEventMessageComposer : IComposer
{
    [Id(0)]
    public required List<BadgePointLimitGroup> LimitsByBadgeCodePrefix { get; init; }
}

[GenerateSerializer, Immutable]
public sealed record BadgePointLimitGroup
{
    [Id(0)]
    public required string BadgeCodePrefix { get; init; }

    [Id(1)]
    public required List<BadgePointLimitLevel> Levels { get; init; }
}

[GenerateSerializer, Immutable]
public sealed record BadgePointLimitLevel
{
    [Id(0)]
    public required int Level { get; init; }

    [Id(1)]
    public required int Limit { get; init; }
}
