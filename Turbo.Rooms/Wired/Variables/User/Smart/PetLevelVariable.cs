using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~pet.level</c> (Wired Faculty, 03/2026): The pet's level. Read only.</summary>
public sealed class PetLevelVariable(RoomGrain roomGrain) : PetStatSmartVariable(roomGrain)
{
    protected override string VariableName => "~pet.level";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) => pet.Level;
}
