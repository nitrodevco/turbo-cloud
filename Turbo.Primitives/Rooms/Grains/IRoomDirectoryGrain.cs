using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Grains;

public interface IRoomDirectoryGrain : IGrainWithStringKey
{
    /// <summary>
    /// The listing keys changed since <paramref name="epoch"/>/<paramref name="sinceSequence"/>,
    /// with live info for every active room when <paramref name="includeActiveRooms"/> is set.
    /// </summary>
    public Task<RoomListingViewSnapshot> GetListingViewAsync(
        Guid epoch,
        long sinceSequence,
        bool includeActiveRooms,
        CancellationToken ct
    );

    /// <summary>Records changes to persisted room data that cached listings must drop.</summary>
    public Task PublishListingChangesAsync(IReadOnlyCollection<string> keys, CancellationToken ct);
    public Task<int> GetRoomPopulationAsync(RoomId roomId, CancellationToken ct);
    public Task UpsertActiveRoomAsync(RoomInfoSnapshot snapshot, CancellationToken ct);

    /// <param name="roomId">The room that deactivated.</param>
    /// <param name="listingChanged">
    /// Whether its navigator data changed while active. The live copy covered it until now, so
    /// the listings it appears in are published as changed.
    /// </param>
    public Task RemoveActiveRoomAsync(RoomId roomId, bool listingChanged, CancellationToken ct);
    public Task AddPlayerToRoomAsync(PlayerId playerId, RoomId roomId, CancellationToken ct);
    public Task RemovePlayerFromRoomAsync(PlayerId playerId, RoomId roomId, CancellationToken ct);
    public Task<RoomId?> GetRandomPopulatedRoomAsync(CancellationToken ct);
}
