using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record IgnoreResultMessageComposer : IComposer
{
    [Id(0)]
    public required MessengerIgnoreResultType Result { get; init; }

    [Id(1)]
    public required PlayerId IgnoredUserId { get; init; }
}
