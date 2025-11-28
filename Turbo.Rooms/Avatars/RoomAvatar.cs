using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms.Avatars;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Avatars;

internal abstract class RoomAvatar : IRoomAvatar
{
    public required string Name { get; init; }
    public string Motto { get; init; } = string.Empty;
    public required string Figure { get; init; }

    public required RoomObjectId ObjectId { get; init; } = RoomObjectId.Empty;
    public required int X { get; init; }
    public required int Y { get; init; }
    public required double Z { get; init; }
    public required Rotation Rotation { get; init; }

    private RoomAvatarSnapshot? _snapshot;
    private bool _dirty = true;

    public RoomAvatarSnapshot GetSnapshot()
    {
        if (_dirty || _snapshot is null)
        {
            _snapshot = BuildSnapshot();
            _dirty = false;
        }

        return _snapshot;
    }

    protected abstract RoomAvatarSnapshot BuildSnapshot();
}
