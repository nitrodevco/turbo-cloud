using Orleans;
using Turbo.Primitives.Achievements.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;

[GenerateSerializer, Immutable]
public sealed record AchievementEventMessageComposer : IComposer
{
    [Id(0)]
    public required AchievementSnapshot Achievement { get; init; }
}
