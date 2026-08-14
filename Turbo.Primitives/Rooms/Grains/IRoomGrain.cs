using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Admin;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain : IGrainWithIntegerKey
{
    public void DeactivateRoom();
    public void DelayRoomDeactivation();
    public Task EnsureRoomActiveAsync(CancellationToken ct);
    public Task<RoomSnapshot> GetSnapshotAsync();
    public Task<RoomSummarySnapshot> GetSummaryAsync();
    public Task<bool> GetIsGroupRoomAsync();
    public Task<int> GetRoomPopulationAsync();
    public Task<ImmutableArray<KeyValuePair<RoomPropertyType, string>>> GetRoomPropertiesAsync();
    public Task PublishRoomEventAsync(RoomEvent evt, CancellationToken ct);
    public Task SendComposerToRoomAsync(IComposer composer);
    public Task AdminUpdateSettingsAsync(RoomAdminSettingsUpdate update, CancellationToken ct);
}
