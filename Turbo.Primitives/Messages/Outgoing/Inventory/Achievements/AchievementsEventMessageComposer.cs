using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots.Achievements;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;

[GenerateSerializer, Immutable]
public sealed record AchievementsEventMessageComposer : IComposer
{
    [Id(0)]
    public required List<PlayerAchievementProgressSnapshot> Achievements { get; init; }
}
