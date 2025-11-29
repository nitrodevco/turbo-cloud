using System.Collections.Generic;
using Turbo.Contracts.Enums.Rooms.Object;
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
    public Rotation Rotation { get; }
    public Rotation HeadRotation { get; }
    public IMovingAvatarLogic Logic { get; }
    public Dictionary<RoomAvatarStatusType, string> Statuses { get; }

    public int GoalTileId { get; set; }
    public int NextTileId { get; set; }
    public int PrevTileId { get; set; }
    public bool IsWalking { get; set; }
    public Queue<int> TilePath { get; }

    public void SetPosition(
        int x,
        int y,
        double z = default,
        Rotation rot = default,
        Rotation headRot = default
    );
    public void SetLogic(IMovingAvatarLogic logic);
    public void AddStatus(RoomAvatarStatusType type, string value);
    public bool HasStatus(params RoomAvatarStatusType[] types);
    public void RemoveStatus(params RoomAvatarStatusType[] types);

    public RoomAvatarSnapshot GetSnapshot();
}
