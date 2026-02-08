using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Notifications;

[GenerateSerializer, Immutable]
public sealed record HabboActivityPointNotificationMessageComposer : IComposer
{
    [Id(0)]
    public required int Amount { get; init; }

    [Id(1)]
    public required int Change { get; init; }

    [Id(2)]
    public required int ActivityPointType { get; init; }
}
