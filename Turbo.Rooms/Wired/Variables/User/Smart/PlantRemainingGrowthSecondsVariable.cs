using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary>
/// <c>~plant.remaining_growth_seconds</c> (Wired Faculty tutorial "Monster Plant growth time FX",
/// 23/09/2026): Seconds until the monsterplant is fully grown, 0 once it is. The tutorial's status
/// bar counts it down from 172800, the two days a plant grows. Read only.
/// </summary>
public sealed class PlantRemainingGrowthSecondsVariable(RoomGrain roomGrain)
    : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.remaining_growth_seconds";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        PetModule.RemainingGrowingSeconds(pet);
}
