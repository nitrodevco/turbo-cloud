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

    /// <summary>
    /// The room the pet was respected in. Only the layout of a client naming
    /// <see cref="Networking.Features.ClientFeatures.PET_INVENTORY_LOCATIONS"/> writes it: there
    /// the pet's data ends with where the pet is.
    /// </summary>
    [Id(3)]
    public PetRoomSnapshot? Room { get; init; }
}
