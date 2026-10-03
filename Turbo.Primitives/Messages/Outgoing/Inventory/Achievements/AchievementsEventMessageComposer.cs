using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Achievements.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;

[GenerateSerializer, Immutable]
public sealed record AchievementsEventMessageComposer : IComposer
{
    [Id(0)]
    public required IReadOnlyList<AchievementSnapshot> Achievements { get; init; }

    [Id(1)]
    public required string DefaultCategory { get; init; }
}
