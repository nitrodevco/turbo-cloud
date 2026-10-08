using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~plant.remaining_wellbeing_seconds</c> (Wired Faculty, 03/2026): Seconds until the monsterplant dies unless it is watered. Read only.</summary>
public sealed class PlantRemainingWellbeingSecondsVariable(RoomGrain roomGrain)
    : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.remaining_wellbeing_seconds";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        PetModule.RemainingWellBeingSeconds(pet);
}
