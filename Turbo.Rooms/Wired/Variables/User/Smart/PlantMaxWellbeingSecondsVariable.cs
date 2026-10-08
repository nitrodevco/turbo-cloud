using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~plant.max_wellbeing_seconds</c> (Wired Faculty, 03/2026): The wellbeing a freshly watered monsterplant has, in seconds. Read only.</summary>
public sealed class PlantMaxWellbeingSecondsVariable(RoomGrain roomGrain)
    : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.max_wellbeing_seconds";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        _roomGrain._petConfig.MonsterplantWellBeingSeconds;
}
