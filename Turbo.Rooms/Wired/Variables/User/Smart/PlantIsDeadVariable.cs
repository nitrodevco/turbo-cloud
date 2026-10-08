using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~plant.is_dead</c> (Wired Faculty, 03/2026): 1 when the monsterplant's wellbeing has run out. Read only.</summary>
public sealed class PlantIsDeadVariable(RoomGrain roomGrain) : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.is_dead";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        PetModule.RemainingWellBeingSeconds(pet) <= 0 ? 1 : 0;
}
