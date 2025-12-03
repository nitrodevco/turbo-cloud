using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomAvatarContext : IRoomObjectContext
{
    public IRoomAvatar Avatar { get; }

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(CancellationToken ct);
}
