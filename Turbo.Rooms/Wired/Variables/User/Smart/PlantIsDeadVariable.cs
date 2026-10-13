using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~plant.is_dead</c> (Wired Faculty, 03/2026): held while the monsterplant's wellbeing has run out. Read only.</summary>
public sealed class PlantIsDeadVariable(RoomGrain roomGrain) : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.is_dead";

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override bool Holds(IRoomPet pet) => PetModule.RemainingWellBeingSeconds(pet) <= 0;

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        WiredVariableValue.Default;
}
