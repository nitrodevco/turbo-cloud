using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record PetRespectNotificationEventMessageComposer : IComposer
{
    [Id(0)]
    public required int Respect { get; init; }

    [Id(1)]
    public required PlayerId PetOwnerId { get; init; }

    [Id(2)]
    public required PetSnapshot Pet { get; init; }
}
