using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~plant.remaining_growing_seconds</c> (Wired Faculty, 03/2026): Seconds until the monsterplant grows a level, 0 once it is grown. Read only.</summary>
public sealed class PlantRemainingGrowingSecondsVariable(RoomGrain roomGrain)
    : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.remaining_growing_seconds";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        PetModule.RemainingGrowingSeconds(pet);
}
