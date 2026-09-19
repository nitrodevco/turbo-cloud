using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Chat;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Grains;

public interface IRoomPersistenceGrain : IGrainWithIntegerKey
{
    public Task EnqueueChatlogAsync(RoomChatlogSnapshot snapshot, CancellationToken ct);
    public Task EnqueueDirtyItemAsync(
        RoomId roomId,
        RoomItemSnapshot snapshot,
        CancellationToken ct,
        bool remove = false
    );

    /// <summary>Deletes the item's row on the next flush instead of updating it.</summary>
    public Task EnqueueDeletedItemAsync(RoomId roomId, RoomObjectId itemId, CancellationToken ct);

    public Task EnqueueDirtyItemsAsync(
        RoomId roomId,
        List<RoomItemSnapshot> snapshots,
        CancellationToken ct
    );

    /// <summary>Writes a placed pet's position and stats on the next flush.</summary>
    public Task EnqueueDirtyPetAsync(PetSnapshot snapshot, CancellationToken ct);

    /// <summary>Writes a placed bot's position and settings on the next flush.</summary>
    public Task EnqueueDirtyBotAsync(BotSnapshot snapshot, CancellationToken ct);
}
