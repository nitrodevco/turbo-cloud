using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Pets;

/// <summary>The account is too young to scratch pets.</summary>
[GenerateSerializer, Immutable]
public sealed record PetRespectFailedMessageComposer : IComposer
{
    [Id(0)]
    public required int RequiredDays { get; init; }

    [Id(1)]
    public required int AvatarAgeDays { get; init; }
}
