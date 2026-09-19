using System.Collections.Generic;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Chat;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Grains;

/// <summary>What the room has changed since the last flush. Everything here is a write buffer.</summary>
internal sealed class RoomPersistenceLiveState
{
    public required RoomId RoomId { get; init; }

    /// <summary>Swapped for an empty one at the start of a flush, so it is settable.</summary>
    public Dictionary<long, RoomItemSnapshot> DirtyItems { get; set; } = [];
    public HashSet<RoomObjectId> RemovedItemIds { get; } = [];
    public HashSet<RoomObjectId> DeletedItemIds { get; } = [];
    public Dictionary<int, PetSnapshot> DirtyPets { get; } = [];
    public Dictionary<int, BotSnapshot> DirtyBots { get; } = [];
    public Queue<RoomChatlogSnapshot> PendingChatlogs { get; } = new();
}
