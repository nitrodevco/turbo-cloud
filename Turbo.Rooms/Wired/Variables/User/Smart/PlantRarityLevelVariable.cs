using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~plant.rarity_level</c> (Wired Faculty, 03/2026): The monsterplant's rarity level. Read only.</summary>
public sealed class PlantRarityLevelVariable(RoomGrain roomGrain) : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.rarity_level";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) => pet.RarityLevel;
}
