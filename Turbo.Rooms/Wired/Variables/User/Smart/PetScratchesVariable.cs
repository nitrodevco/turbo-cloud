using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~pet.scratches</c> (Wired Faculty, 03/2026): How often the pet has been scratched (its respect). Read only.</summary>
public sealed class PetScratchesVariable(RoomGrain roomGrain) : PetStatSmartVariable(roomGrain)
{
    protected override string VariableName => "~pet.scratches";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) => pet.Respect;
}
