using System.Linq;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// <c>@pet_owner_id</c> (hhus, WIN63-202610121709, id -280): the user id of a pet's owner, held by pets only. Read
/// only. Habbo lists it invisible while no pet is in the room, as it does a smart variable.
/// </summary>
public sealed class UserPetOwnerIdVariable(RoomGrain roomGrain)
    : UserValueVariable<IRoomPet>(roomGrain),
        IWiredSmartVariable
{
    public bool IsPresent() => PetModule.Pets.Any();

    protected override string VariableName => "@pet_owner_id";

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected override WiredVariableValue GetValueForAvatar(IRoomPet avatar) =>
        WiredVariableValue.Parse(avatar.OwnerId.Value);
}
