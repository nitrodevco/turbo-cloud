using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~pet.energy</c> (Wired Faculty, 03/2026): The pet's energy. Read only.</summary>
public sealed class PetEnergyVariable(RoomGrain roomGrain) : PetStatSmartVariable(roomGrain)
{
    protected override string VariableName => "~pet.energy";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) => pet.Energy;
}
