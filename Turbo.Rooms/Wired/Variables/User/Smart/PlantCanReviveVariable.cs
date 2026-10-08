using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~plant.can_revive</c> (Wired Faculty, 03/2026): 1 when the monsterplant can be revived, which is when it has died. Read only.</summary>
public sealed class PlantCanReviveVariable(RoomGrain roomGrain) : PlantSmartVariable(roomGrain)
{
    protected override string VariableName => "~plant.can_revive";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) => pet.CanRevive ? 1 : 0;
}
