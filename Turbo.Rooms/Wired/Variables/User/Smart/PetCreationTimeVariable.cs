using System;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~pet.creation_time</c> (Wired Faculty, 03/2026): When the pet was made, in unix milliseconds like <c>@current_time</c>. Read only.</summary>
public sealed class PetCreationTimeVariable(RoomGrain roomGrain) : PetStatSmartVariable(roomGrain)
{
    protected override string VariableName => "~pet.creation_time";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) =>
        new DateTimeOffset(
            DateTime.SpecifyKind(pet.CreatedAtUtc, DateTimeKind.Utc)
        ).ToUnixTimeMilliseconds();
}
