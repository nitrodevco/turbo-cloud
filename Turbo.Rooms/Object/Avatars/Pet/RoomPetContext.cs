using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object.Avatars.Pet;

public sealed class RoomPetContext(RoomGrain roomGrain, IRoomPet roomObject)
    : RoomAvatarContext<IRoomPet, IRoomPetLogic, IRoomPetContext>(roomGrain, roomObject),
        IRoomPetContext
{
    IRoomPet IRoomPetContext.RoomObject => RoomObject;
}
