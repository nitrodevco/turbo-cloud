using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomAvatar<TSelf, out TLogic, out TContext>
    : IRoomObject<TSelf, TLogic, TContext>,
        IRoomAvatar
    where TSelf : IRoomAvatar<TSelf, TLogic, TContext>
    where TContext : IRoomAvatarContext<TSelf, TLogic, TContext>
    where TLogic : IRoomAvatarLogic<TSelf, TLogic, TContext>
{
    new TLogic Logic { get; }
}

public interface IRoomAvatar : IRoomObject
{
    new IRoomAvatarLogic Logic { get; }
    public RoomObjectType AvatarType { get; }
    public string Name { get; }
    public string Motto { get; }
    public string Figure { get; }
    public Rotation HeadRotation { get; }
    public int JumpPower { get; }
    public Dictionary<AvatarStatusType, string> Statuses { get; }

    public Altitude PostureOffset { get; set; }
    public int GoalTileId { get; }
    public int NextTileId { get; set; }
    public bool IsWalking { get; set; }
    public bool NeedsInvoke { get; set; }
    public List<int> TilePath { get; }

    public long NextMoveStepAtMs { get; set; }
    public long NextMoveUpdateAtMs { get; set; }
    public long PendingStopAtMs { get; set; }

    /// <summary>The hand item (drink, snack) the avatar carries, 0 for none.</summary>
    public int HandItemId { get; }

    /// <summary>Milliseconds (room clock) of the avatar's last deliberate action.</summary>
    public long LastActiveAtMs { get; }
    public bool IsIdle { get; }

    /// <summary>Frozen avatars cannot walk. Whatever freezes one also thaws it.</summary>
    public bool IsFrozen { get; }

    /// <summary>Whether being teleported thaws a frozen avatar. Set with the freeze and gone with it.</summary>
    public bool ThawsOnTeleport { get; }
    public bool SetGoalTileId(int tileId);
    public bool SetHandItem(int handItemId);
    public void Touch(long nowMs);
    public void SetIdle(bool isIdle);
    public void SetFrozen(bool isFrozen, bool thawsOnTeleport = false);
    public void SetHeight(Altitude z);
    public void SetBodyRotation(Rotation rot);
    public void SetHeadRotation(Rotation rot);
    public void Sit(bool flag = true, Altitude? height = null, Rotation? rot = null);
    public void Lay(bool flag = true, Altitude? height = null, Rotation? rot = null);

    public void AddStatus(AvatarStatusType type, string value);
    public bool HasStatus(params AvatarStatusType[] types);
    public void RemoveStatus(params AvatarStatusType[] types);

    public RoomAvatarSnapshot GetSnapshot();
}
