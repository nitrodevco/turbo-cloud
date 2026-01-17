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
    public abstract RoomObjectType AvatarType { get; }

    public string Name { get; init; } = string.Empty;
    public string Motto { get; init; } = string.Empty;
    public string Figure { get; init; } = string.Empty;

    public Rotation HeadRotation { get; protected set; }
    public AvatarDanceType DanceType { get; private set; } = AvatarDanceType.None;
    public IRoomAvatarLogic Logic { get; private set; } = default!;
    public Dictionary<AvatarStatusType, string> Statuses { get; } = [];

    public double PostureOffset { get; set; } = 0.0;
    public int GoalTileId { get; private set; } = -1;
    public int NextTileId { get; set; } = -1;
    public bool IsWalking { get; set; } = false;
    public bool NeedsInvoke { get; set; } = false;
    public List<int> TilePath { get; } = [];

    public long NextMoveStepAtMs { get; set; } = 0;
    public long NextMoveUpdateAtMs { get; set; } = 0;
    public long PendingStopAtMs { get; set; } = 0;

    private int _goalTries = 0;

    private RoomAvatarSnapshot? _snapshot;

    public bool SetGoalTileId(int tileId)
    {
        if (tileId == -1)
        {
            GoalTileId = -1;
            _goalTries = 0;

            return true;
        }

        if (tileId == GoalTileId)
        {
            _goalTries++;
        }
        else
        {
            GoalTileId = tileId;
            _goalTries = 0;
        }

        if (_goalTries == 3)
            return false;

        return true;
    }

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
        z = Math.Round(z, 3);

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
        if (Rotation == rot)
            return;

        Rotation = rot;

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

            rot ??= Rotation;

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

            rot ??= Rotation;

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
