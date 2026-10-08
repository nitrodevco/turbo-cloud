using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~pet.max_happiness</c> (Wired Faculty, 03/2026): The most happiness (nutrition) a pet can have. Read only.</summary>
public sealed class PetMaxHappinessVariable(RoomGrain roomGrain) : PetStatSmartVariable(roomGrain)
{
    protected override string VariableName => "~pet.max_happiness";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        _roomGrain._petConfig.MaxNutrition;
}
