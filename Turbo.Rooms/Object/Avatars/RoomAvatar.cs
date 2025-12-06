using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Enums;
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
    public Rotation BodyRotation { get; protected set; }
    public Rotation HeadRotation { get; protected set; }
    public AvatarDanceType DanceType { get; private set; } = AvatarDanceType.None;
    public IRoomAvatarLogic Logic { get; private set; } = default!;
    public Dictionary<AvatarStatusType, string> Statuses { get; } = [];

    public int GoalTileId { get; set; } = -1;
    public int NextTileId { get; set; } = -1;
    public int PrevTileId { get; set; } = -1;
    public bool IsWalking { get; set; } = false;
    public List<int> TilePath { get; } = [];

    private RoomAvatarSnapshot? _snapshot;

    public void SetPosition(int x, int y)
    {
        if (X == x && Y == y)
            return;

        X = x;
        Y = y;

        MarkDirty();
    }

    public void SetHeight(double z)
    {
        z = Math.Truncate(z * 1000) / 1000;

        if (Z == z)
            return;

        Z = z;

        MarkDirty();
    }

    public void SetRotation(Rotation rot)
    {
        SetBodyRotation(rot);
        SetHeadRotation(rot);
    }

    public void SetBodyRotation(Rotation rot)
    {
        if (BodyRotation == rot)
            return;

        BodyRotation = rot;

        MarkDirty();
    }

    public void SetHeadRotation(Rotation rot)
    {
        if (HeadRotation == rot)
            return;

        HeadRotation = rot;

        MarkDirty();
    }

    public bool SetDance(AvatarDanceType danceType = AvatarDanceType.None)
    {
        if (DanceType == danceType)
            return false;

        if (HasStatus(AvatarStatusType.Sit, AvatarStatusType.Lay))
            return false;

        // check if dance valid
        // check if dance is hc only / validate hc

        DanceType = danceType;

        return true;
    }

    public void SetLogic(IRoomAvatarLogic logic)
    {
        Logic = logic;
    }

    public void Sit(bool flag = true, double height = 0.5, Rotation? rot = null)
    {
        if (flag)
        {
            // remove dance
            RemoveStatus(AvatarStatusType.Lay);

            rot ??= BodyRotation;

            SetRotation(rot.Value.ToSitRotation());
            AddStatus(AvatarStatusType.Sit, height.ToString());
        }
        else
        {
            if (!HasStatus(AvatarStatusType.Sit))
                return;

            RemoveStatus(AvatarStatusType.Sit);
        }
    }

    public void Lay(bool flag = true, double height = 0.5, Rotation? rot = null)
    {
        if (flag)
        {
            // remove dance
            RemoveStatus(AvatarStatusType.Sit);

            rot ??= BodyRotation;

            SetRotation(rot.Value.ToSitRotation());
            AddStatus(AvatarStatusType.Sit, height.ToString());
        }
        else
        {
            if (!HasStatus(AvatarStatusType.Lay))
                return;

            RemoveStatus(AvatarStatusType.Lay);
        }
    }

    public void AddStatus(AvatarStatusType type, string value)
    {
        Statuses[type] = value;

        MarkDirty();
    }

    public bool HasStatus(params AvatarStatusType[] types) =>
        types.Any(x => Statuses.ContainsKey(x));

    public void RemoveStatus(params AvatarStatusType[] types)
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
