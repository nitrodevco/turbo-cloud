using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record HandItemReceivedMessageComposer : IComposer
{
    [Id(0)]
    public required PlayerId GiverPlayerId { get; init; }

    [Id(1)]
    public required int HandItemType { get; init; }
}
