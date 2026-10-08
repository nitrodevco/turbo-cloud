using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~plant.is_growing</c> (Wired Faculty, 03/2026): 1 while the monsterplant has levels left to grow. Read only.</summary>
public sealed class PlantIsGrowingVariable(RoomGrain roomGrain) : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.is_growing";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        PetModule.RemainingGrowingSeconds(pet) > 0 ? 1 : 0;
}
