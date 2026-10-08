using Turbo.Primitives.Pets;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary>
/// <c>~plant.shape</c> (Wired Faculty, 03/2026): The monsterplant's body type, 1 to 12 as the
/// monsterplant asset numbers its body parts; 0 for a plant drawn with the default body. Read
/// only.
/// </summary>
public sealed class PlantShapeVariable(RoomGrain roomGrain) : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.shape";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        MonsterplantFigure.Shape(pet.PetFigure);
}
