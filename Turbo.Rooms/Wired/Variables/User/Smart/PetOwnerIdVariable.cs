using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~pet.owner_id</c> (Wired Faculty, 03/2026): The user id of the pet's owner, as <c>@user_id</c> reports a user's. Read only.</summary>
public sealed class PetOwnerIdVariable(RoomGrain roomGrain) : PetStatSmartVariable(roomGrain)
{
    protected override string VariableName => "~pet.owner_id";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) => pet.OwnerId.Value;
}
