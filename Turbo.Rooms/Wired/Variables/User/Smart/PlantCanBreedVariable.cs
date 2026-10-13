using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~plant.can_breed</c> (Wired Faculty, 03/2026): held while the monsterplant can be bred: grown and alive. Read only.</summary>
public sealed class PlantCanBreedVariable(RoomGrain roomGrain) : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.can_breed";

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override bool Holds(IRoomPet pet) => pet.CanBreed;

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        WiredVariableValue.Default;
}
