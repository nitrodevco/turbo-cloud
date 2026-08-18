using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Notifications;

[GenerateSerializer, Immutable]
public sealed record HabboAchievementNotificationMessageComposer : IComposer
{
    [Id(0)]
    public required string Code { get; init; }

    [Id(1)]
    public required int Level { get; init; }

    [Id(2)]
    public required int Progress { get; init; }

    [Id(3)]
    public required int LevelGoal { get; init; }
}
