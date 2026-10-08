using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User.Smart;

/// <summary><c>~horse.controller_user_id</c> (Wired Faculty, 03/2026): The user id of whoever rides the horse, 0 while nobody does. Read only.</summary>
public sealed class HorseControllerUserIdVariable(RoomGrain roomGrain)
    : HorseSmartVariable(roomGrain)
{
    protected override string VariableName => "~horse.controller_user_id";

    protected override WiredVariableValue GetValueForAvatar(IRoomPet pet) => RiderUserId(pet);

    private long RiderUserId(IRoomPet pet) =>
        pet.IsRiding
        && AvatarModule.TryGetAvatar(pet.RiderObjectId, out var rider)
        && rider is IRoomPlayer player
            ? player.PlayerId.Value
            : 0;
}
