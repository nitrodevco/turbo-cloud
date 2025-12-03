using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;

[GenerateSerializer, Immutable]
public sealed record AchievementsScoreEventMessageComposer : IComposer
{
    [Id(0)]
    public required int Score { get; init; }
}
