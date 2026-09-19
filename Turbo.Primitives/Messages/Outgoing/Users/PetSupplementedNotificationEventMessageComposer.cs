using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record PetSupplementedNotificationEventMessageComposer : IComposer
{
    [Id(0)]
    public required int PetId { get; init; }

    [Id(1)]
    public required PlayerId PlayerId { get; init; }

    [Id(2)]
    public required PetSupplementType Supplement { get; init; }
}
