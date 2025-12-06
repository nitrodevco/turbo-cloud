using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomAvatar : IRoomObject
{
    public string Name { get; }
    public string Motto { get; }
    public string Figure { get; }
    public int X { get; }
    public int Y { get; }
    public double Z { get; }
    public Rotation BodyRotation { get; }
    public Rotation HeadRotation { get; }
    public IRoomAvatarLogic Logic { get; }
    public Dictionary<AvatarStatusType, string> Statuses { get; }

    public AvatarDanceType DanceType { get; }

    public int GoalTileId { get; }
    public int NextTileId { get; set; }
    public int PrevTileId { get; set; }
    public bool IsWalking { get; set; }
    public List<int> TilePath { get; }

    public bool SetGoalTileId(int tileId);
    public void SetPosition(int x, int y);
    public void SetHeight(double z);
    public void SetRotation(Rotation rot);
    public void SetBodyRotation(Rotation rot);
    public void SetHeadRotation(Rotation rot);
    public void SetLogic(IRoomAvatarLogic logic);
    public bool SetDance(AvatarDanceType danceType = AvatarDanceType.None);
    public void Sit(bool flag = true, double height = 0.5, Rotation? rot = null);
    public void Lay(bool flag = true, double height = 0.5, Rotation? rot = null);

    public void AddStatus(AvatarStatusType type, string value);
    public bool HasStatus(params AvatarStatusType[] types);
    public void RemoveStatus(params AvatarStatusType[] types);

    public RoomAvatarSnapshot GetSnapshot();
}
