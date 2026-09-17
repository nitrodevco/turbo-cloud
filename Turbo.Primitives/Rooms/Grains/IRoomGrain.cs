using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain : IGrainWithIntegerKey
{
    public void DeactivateRoom();
    public void DelayRoomDeactivation();
    public Task EnsureRoomActiveAsync(CancellationToken ct);
    public Task<RoomSnapshot> GetSnapshotAsync(CancellationToken ct);
    public Task<RoomSummarySnapshot> GetSummaryAsync(CancellationToken ct);
    public Task<bool> GetIsGroupRoomAsync(CancellationToken ct);
    public Task<int> GetRoomPopulationAsync(CancellationToken ct);
    public Task<ImmutableArray<KeyValuePair<RoomPropertyType, string>>> GetRoomPropertiesAsync(
        CancellationToken ct
    );
    public Task PublishRoomEventAsync(RoomEvent evt, CancellationToken ct);
    public Task SendComposerToRoomAsync(IComposer composer, CancellationToken ct);
}
