using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~pet.experience_required</c> (Wired Faculty, 03/2026): The experience the pet needs to leave its level, as its info stand shows it. Read only.</summary>
public sealed class PetExperienceRequiredVariable(RoomGrain roomGrain)
    : PetStatSmartVariable(roomGrain)
{
    protected override string VariableName => "~pet.experience_required";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        PetModule.ExperienceToLevel(pet.Level);
}
