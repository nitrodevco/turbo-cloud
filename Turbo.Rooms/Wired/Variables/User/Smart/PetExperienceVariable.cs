using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~pet.experience</c> (Wired Faculty, 03/2026): The pet's experience. Read only.</summary>
public sealed class PetExperienceVariable(RoomGrain roomGrain) : PetStatSmartVariable(roomGrain)
{
    protected override string VariableName => "~pet.experience";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) => pet.Experience;
}
