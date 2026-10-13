using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~plant.can_harvest</c> (Wired Faculty, 03/2026): held while the monsterplant can be harvested. Read only.</summary>
public sealed class PlantCanHarvestVariable(RoomGrain roomGrain) : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.can_harvest";

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override bool Holds(IRoomPet pet) => pet.CanHarvest;

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        WiredVariableValue.Default;
}
