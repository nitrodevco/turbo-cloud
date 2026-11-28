using Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Pets.Snapshots;

/// <summary>
/// The room a placed pet stands in, as the pets tab of a client naming
/// <see cref="Networking.Features.ClientFeatures.PET_INVENTORY_LOCATIONS"/> shows it beside the
/// pet: the client reads the name and description only for a pet that has a room.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PetRoomSnapshot
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required string Description { get; init; }
}
