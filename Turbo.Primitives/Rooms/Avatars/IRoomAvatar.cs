using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Primitives.Rooms.Avatars;

public interface IRoomAvatar
{
    public RoomObjectId ObjectId { get; }
    public string Name { get; }
    public string Motto { get; }
    public string Figure { get; }
    public int X { get; }
    public int Y { get; }
    public double Z { get; }
    public Rotation Rotation { get; }
    public IMovingAvatarLogic Logic { get; }

    public void SetPosition(int x, int y, double z, Rotation rot);
    public void SetLogic(IMovingAvatarLogic logic);

    public RoomAvatarSnapshot GetSnapshot();
}
