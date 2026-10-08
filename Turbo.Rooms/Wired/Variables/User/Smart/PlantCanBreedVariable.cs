using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~plant.can_breed</c> (Wired Faculty, 03/2026): 1 when the monsterplant can be bred: grown and alive. Read only.</summary>
public sealed class PlantCanBreedVariable(RoomGrain roomGrain) : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.can_breed";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) => pet.CanBreed ? 1 : 0;
}
