using System.Collections.Generic;
using System.Linq;
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
    public Rotation HeadRotation { get; protected set; }
    public IMovingAvatarLogic Logic { get; private set; } = default!;
    public Dictionary<RoomAvatarStatusType, string> Statuses { get; } = [];

    private RoomAvatarSnapshot? _snapshot;

    public void SetPosition(int x, int y, double z, Rotation rot, Rotation headRot)
    {
        if (X == x && Y == y && Z == z && rot == Rotation && headRot == HeadRotation)
            return;

        X = x;
        Y = y;
        Z = z;
        Rotation = rot;
        HeadRotation = headRot;

        MarkDirty();
    }

    public void SetLogic(IMovingAvatarLogic logic)
    {
        Logic = logic;
    }

    public void AddStatus(RoomAvatarStatusType type, string value)
    {
        Statuses[type] = value;

        MarkDirty();
    }

    public bool HasStatus(params RoomAvatarStatusType[] types) =>
        types.Any(x => Statuses.ContainsKey(x));

    public void RemoveStatus(params RoomAvatarStatusType[] types)
    {
        if (types.Length == 0)
            return;

        var updated = false;

        foreach (var type in types)
        {
            if (Statuses.Remove(type))
                updated = true;
        }

        if (updated)
            MarkDirty();
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
