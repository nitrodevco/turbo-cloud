using Turbo.Primitives.Pets;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary>
/// <c>~plant.shape</c> (Wired Faculty, 03/2026): The monsterplant's body type, 0 to 11 (the
/// asset numbers its body parts 1 to 12; wired counts from 0, as for <c>~plant.color</c>); 0 for
/// a plant drawn with the default body. Read only.
/// </summary>
public sealed class PlantShapeVariable(RoomGrain roomGrain) : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.shape";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        MonsterplantFigure.Shape(pet.PetFigure);
}
