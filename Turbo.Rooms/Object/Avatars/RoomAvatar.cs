using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Rooms.Object.Avatars;

public abstract class RoomAvatar<TSelf, TLogic, TContext>
    : RoomObject<TSelf, TLogic, TContext>,
        IRoomAvatar<TSelf, TLogic, TContext>
    where TSelf : IRoomAvatar<TSelf, TLogic, TContext>
    where TContext : IRoomAvatarContext<TSelf, TLogic, TContext>
    where TLogic : IRoomAvatarLogic<TSelf, TLogic, TContext>
{
    IRoomAvatarLogic IRoomAvatar.Logic => Logic;

    public abstract RoomObjectType AvatarType { get; }

    public string Name { get; protected set; } = string.Empty;
    public string Motto { get; protected set; } = string.Empty;
    public string Figure { get; protected set; } = string.Empty;

    public Rotation HeadRotation { get; protected set; }
    public int JumpPower { get; protected set; }
    public Dictionary<AvatarStatusType, string> Statuses { get; } = [];

    public Altitude PostureOffset { get; set; } = Altitude.Zero;
    public int GoalTileId { get; private set; } = -1;
    public int NextTileId { get; set; } = -1;
    public bool IsWalking { get; set; } = false;
    public bool NeedsInvoke { get; set; } = false;
    public List<int> TilePath { get; } = [];

    public long NextMoveStepAtMs { get; set; } = 0;
    public long NextMoveUpdateAtMs { get; set; } = 0;
    public long PendingStopAtMs { get; set; } = 0;
    public int HandItemId { get; private set; } = 0;
    public AvatarDanceType DanceType { get; private set; } = AvatarDanceType.None;
    public int EffectId { get; private set; } = 0;
    public long LastActiveAtMs { get; private set; } = 0;
    public bool IsIdle { get; private set; } = false;
    public bool IsFrozen { get; private set; } = false;
    public bool ThawsOnTeleport { get; private set; } = false;

    private int _goalTries = 0;

    protected RoomAvatarSnapshot? _snapshot;

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

    public void SetHeight(Altitude z)
    {
        z = Math.Round(z, 2);

        if (Z == z)
            return;

        Z = z;

        MarkDirty();
    }

    public new void SetRotation(Rotation rot)
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

    public virtual void Sit(bool flag = true, Altitude? height = null, Rotation? rot = null)
    {
        var finalHeight = height ?? Altitude.FromValue(0.5);

        if (flag)
        {
            RemoveStatus(AvatarStatusType.Lay);

            // Nothing dances sitting down. Every kind of avatar did this in its own override
            // before; the posture is the base's to keep, so the dance it cancels is too.
            SetDance(AvatarDanceType.None);

            rot ??= Rotation;

            SetRotation(rot.Value.ToSitRotation());
            AddStatus(AvatarStatusType.Sit, finalHeight.ToString());
        }
        else
        {
            if (!HasStatus(AvatarStatusType.Sit))
                return;

            RemoveStatus(AvatarStatusType.Sit);
        }
    }

    public virtual void Lay(bool flag = true, Altitude? height = null, Rotation? rot = null)
    {
        var finalHeight = height ?? Altitude.FromValue(0.5);

        if (flag)
        {
            RemoveStatus(AvatarStatusType.Sit);

            SetDance(AvatarDanceType.None);

            rot ??= Rotation;

            SetRotation(rot.Value.ToSitRotation());
            AddStatus(AvatarStatusType.Lay, finalHeight.ToString());
        }
        else
        {
            if (!HasStatus(AvatarStatusType.Lay))
                return;

            RemoveStatus(AvatarStatusType.Lay);
        }
    }

    public bool SetHandItem(int handItemId)
    {
        if (handItemId < 0 || handItemId == HandItemId)
            return false;

        HandItemId = handItemId;

        if (handItemId == 0)
            RemoveStatus(AvatarStatusType.CarryItem);
        else
            AddStatus(AvatarStatusType.CarryItem, handItemId.ToString());

        return true;
    }

    /// <summary>
    /// Every avatar dances the same way, so this lives here rather than once per kind. Stopping
    /// is always allowed; only starting is refused to an avatar that is sitting or lying.
    /// </summary>
    public bool SetDance(AvatarDanceType danceType = AvatarDanceType.None)
    {
        if (DanceType == danceType)
            return false;

        if (
            danceType != AvatarDanceType.None
            && HasStatus(AvatarStatusType.Sit, AvatarStatusType.Lay)
        )
            return false;

        DanceType = danceType;

        DropCachedSnapshot();

        return true;
    }

    public bool SetEffect(int effectId = 0)
    {
        if (effectId < 0 || EffectId == effectId)
            return false;

        EffectId = effectId;

        DropCachedSnapshot();

        return true;
    }

    /// <summary>
    /// Throws away the cached snapshot without marking the avatar dirty. A dance and an effect
    /// each travel in their own composer, so the room has already been told; marking dirty would
    /// put the avatar in the tick's <c>UserUpdate</c> as well and say it a second time. The
    /// snapshot still has to be rebuilt, because it is what a player arriving later reads.
    /// </summary>
    private void DropCachedSnapshot() => _snapshot = null;

    public void Touch(long nowMs) => LastActiveAtMs = nowMs;

    public void SetIdle(bool isIdle) => IsIdle = isIdle;

    public void SetFrozen(bool isFrozen, bool thawsOnTeleport = false)
    {
        IsFrozen = isFrozen;
        ThawsOnTeleport = isFrozen && thawsOnTeleport;
    }

    public void AddStatus(AvatarStatusType type, string value)
    {
        Statuses[type] = value;

        MarkDirty();
    }

    public bool HasStatus(params AvatarStatusType[] types) => types.Any(Statuses.ContainsKey);

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
