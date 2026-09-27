using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record BlockUserUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required MessengerBlockResultType Result { get; init; }

    [Id(1)]
    public required int UserId { get; init; }
}
