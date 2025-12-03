using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Notifications;

[GenerateSerializer, Immutable]
public sealed record InfoFeedEnableMessageComposer : IComposer
{
    [Id(0)]
    public required bool Enabled { get; init; }
}
