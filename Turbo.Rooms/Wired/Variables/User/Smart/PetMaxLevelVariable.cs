using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~pet.max_level</c> (Wired Faculty, 03/2026): The highest level the pet can reach. Read only.</summary>
public sealed class PetMaxLevelVariable(RoomGrain roomGrain) : PetStatSmartVariable(roomGrain)
{
    protected override string VariableName => "~pet.max_level";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        PetModule.MaxLevelFor(pet);
}
