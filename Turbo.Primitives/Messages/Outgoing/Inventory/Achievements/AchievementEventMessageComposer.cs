using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots.Achievements;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;

[GenerateSerializer, Immutable]
public sealed record AchievementEventMessageComposer : IComposer
{
    [Id(0)]
    public required PlayerAchievementProgressSnapshot Achievement { get; init; }
}
