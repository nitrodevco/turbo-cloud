using Turbo.Primitives.Pets;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary>
/// <c>~plant.color</c> (Wired Faculty, 03/2026): The monsterplant's colour, the palette its body
/// is drawn in (0 to 10, <c>mnstr_pal1</c> to <c>mnstr_pal11</c>). Read only.
/// </summary>
public sealed class PlantColorVariable(RoomGrain roomGrain) : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.color";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        MonsterplantFigure.Color(pet.PetFigure);
}
