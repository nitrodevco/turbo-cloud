using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Object.Avatars;

internal abstract class RoomAvatar : RoomObject, IRoomAvatar
{
    public string Name { get; init; } = string.Empty;
    public string Motto { get; init; } = string.Empty;
    public string Figure { get; init; } = string.Empty;

    public int X { get; protected set; }
    public int Y { get; protected set; }
    public double Z { get; protected set; }
    public Rotation Rotation { get; protected set; }
    public IMovingAvatarLogic Logic { get; private set; } = default!;

    private RoomAvatarSnapshot? _snapshot;

    public void SetPosition(int x, int y, double z, Rotation rot)
    {
        if (X == x && Y == y && Z == z && rot == Rotation)
            return;

        X = x;
        Y = y;
        Z = z;
        Rotation = rot;

        MarkDirty();
    }

    public void SetLogic(IMovingAvatarLogic logic)
    {
        Logic = logic;
    }

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
