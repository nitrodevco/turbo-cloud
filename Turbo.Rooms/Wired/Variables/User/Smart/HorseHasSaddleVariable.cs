using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~horse.has_saddle</c> (Wired Faculty, 03/2026): held while the horse wears a saddle. Read only.</summary>
public sealed class HorseHasSaddleVariable(RoomGrain roomGrain) : HorseSmartVariable(roomGrain)
{
    protected override string VariableName => "~horse.has_saddle";

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override bool Holds(IRoomPet pet) => pet.HasSaddle;

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        WiredVariableValue.Default;
}
