using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~horse.is_riding</c> (Wired Faculty, 03/2026): 1 while someone rides the horse. Read only.</summary>
public sealed class HorseIsRidingVariable(RoomGrain roomGrain) : HorseSmartVariable(roomGrain)
{
    protected override string VariableName => "~horse.is_riding";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) => pet.IsRiding ? 1 : 0;
}
