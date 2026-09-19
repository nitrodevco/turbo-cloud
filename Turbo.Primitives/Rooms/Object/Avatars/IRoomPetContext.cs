using Turbo.Primitives.Rooms.Object.Logic.Avatars;

namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomPetContext : IRoomAvatarContext<IRoomPet, IRoomPetLogic, IRoomPetContext>
{
    new IRoomPet RoomObject { get; }
}
