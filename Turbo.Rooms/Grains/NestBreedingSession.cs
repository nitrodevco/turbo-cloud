using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains;

/// <summary>
/// Two pets sitting in a breeding nest, waiting for every involved owner to name the offspring
/// and confirm. One owner breeding two of their own pets confirms once.
/// </summary>
public sealed class NestBreedingSession
{
    public required RoomObjectId NestId { get; init; }
    public required int Pet1Id { get; init; }
    public required int Pet2Id { get; init; }
    public required HashSet<PlayerId> OwnerIds { get; init; }
    public HashSet<PlayerId> ConfirmedOwnerIds { get; } = [];
    public string? Name { get; set; }

    public bool Involves(int petId) => petId == Pet1Id || petId == Pet2Id;

    public bool IsConfirmedByAll => ConfirmedOwnerIds.IsSupersetOf(OwnerIds);
}
