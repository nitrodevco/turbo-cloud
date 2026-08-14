using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record BlockUserUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required int Result { get; init; }

    [Id(1)]
    public required int UserId { get; init; }
}
