using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Badges.Snapshots;

/// <summary>
/// The point limits of one family of badges, named by the prefix its codes share (the client
/// matches a badge code against the prefix and reads off the level it has reached).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record BadgePointLimitGroupSnapshot
{
    [Id(0)]
    public required string BadgeCodePrefix { get; init; }

    [Id(1)]
    public required List<BadgePointLimitLevelSnapshot> Levels { get; init; }
}
