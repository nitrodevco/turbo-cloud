using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~pet.happiness</c> (Wired Faculty, 03/2026): The pet's happiness: its nutrition, which the info stand's happiness bar shows (<c>InfoStandPetView</c>). Read only.</summary>
public sealed class PetHappinessVariable(RoomGrain roomGrain) : PetStatSmartVariable(roomGrain)
{
    protected override string VariableName => "~pet.happiness";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) => pet.Nutrition;
}
