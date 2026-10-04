using System;
using System.Collections.Generic;
using System.Text;
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
    public bool IsTeleporting { get; set; } = false;
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

    /// <summary>How many times one walk may find a new way round a step blocked on the way.</summary>
    private const int MAX_GOAL_REROUTES = 3;

    /// <summary>
    /// Sets the goal of a new walk, or clears it with -1, and starts its re-route count over.
    /// Every walk request starts here: a click on a tile the avatar walked to, or was refused,
    /// a moment ago is a new walk, not a re-route of the old one.
    /// </summary>
    public void SetGoalTileId(int tileId)
    {
        GoalTileId = tileId;
        _goalTries = 0;
    }

    /// <summary>Spends one of the current walk's re-routes; false once they are used up.</summary>
    public bool TryRerouteGoal() => GoalTileId >= 0 && ++_goalTries < MAX_GOAL_REROUTES;

    /// <summary>
    /// Turns the whole avatar: the head follows the body. This is an override and not a second
    /// method, because an avatar reached through <see cref="IRoomObject"/> must turn the same way
    /// as one reached through its own type; while it was hidden with <c>new</c>, a walking avatar
    /// turned its body and left its head where it was.
    /// </summary>
    public override void SetRotation(Rotation rot)
    {
        SetBodyRotation(rot);
        SetHeadRotation(rot);
    }

    /// <summary>Turns the body and leaves the head where it is (a look-at, a pet glancing round).</summary>
    public void SetBodyRotation(Rotation rot) => base.SetRotation(rot);

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

    /// <summary>
    /// The statuses as the client's <c>Users</c> and <c>UserUpdate</c> packets carry them:
    /// <c>/key value/key value/</c>. Every kind of avatar writes its snapshot with this.
    /// </summary>
    protected string BuildStatusString()
    {
        var status = new StringBuilder("/");

        foreach (var (type, value) in Statuses)
            status.Append(type.ToLegacyString()).Append(' ').Append(value).Append('/');

        return status.ToString();
    }

    public void AddStatus(AvatarStatusType type, string value)
    {
        Statuses[type] = value;

        MarkDirty();
    }

    // Spans, not arrays: these run for every avatar on every step, and a params array was a
    // fresh allocation per call.
    public bool HasStatus(params ReadOnlySpan<AvatarStatusType> types)
    {
        foreach (var type in types)
        {
            if (Statuses.ContainsKey(type))
                return true;
        }

        return false;
    }

    public void RemoveStatus(params ReadOnlySpan<AvatarStatusType> types)
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
