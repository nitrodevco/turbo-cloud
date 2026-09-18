using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record RespectNotificationMessageComposer : IComposer
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required int RespectTotal { get; init; }
}
