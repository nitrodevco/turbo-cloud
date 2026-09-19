using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;

namespace Turbo.Rooms.Object.Logic.Avatar;

/// <summary>Behaviour runs in <c>RoomPetTickSystem</c>; the logic carries the object lifecycle.</summary>
[RoomObjectLogic("default_pet")]
public sealed class PetLogic(IRoomPetContext ctx)
    : AvatarLogic<IRoomPet, IRoomPetLogic, IRoomPetContext>(ctx),
        IRoomPetLogic { }
