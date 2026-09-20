using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// The pet's own id, the one its row is kept under, which outlives its being in this room.
/// Only a pet holds it: a player or a bot has none.
/// </summary>
public sealed class UserPetIdVariable(RoomGrain roomGrain) : UserVariable<IRoomPet>(roomGrain)
{
    protected override string VariableName => "@pet_id";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Other;

    protected override ushort Order => 80;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected override WiredVariableValue GetValueForAvatar(IRoomPet avatar) =>
        WiredVariableValue.Parse(avatar.PetId);
}
