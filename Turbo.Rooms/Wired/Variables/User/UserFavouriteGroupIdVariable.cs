using System.Diagnostics.CodeAnalysis;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// <c>@favourite_group_id</c> (spelt so in the official client): the group the player wears the
/// badge of. Read only; held only while they have one.
/// </summary>
public sealed class UserFavouriteGroupIdVariable(RoomGrain roomGrain)
    : UserValueVariable<IRoomPlayer>(roomGrain)
{
    protected override string VariableName => "@favourite_group_id";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    // After @is_trading (80), before @dance (60), as the official list has it.
    protected override ushort Order => 70;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected override bool TryGetAvatarForKey(
        in WiredVariableKey key,
        [NotNullWhen(true)] out IRoomPlayer? avatar
    ) => base.TryGetAvatarForKey(key, out avatar) && avatar.GuildId > 0;

    protected override WiredVariableValue GetValueForAvatar(IRoomPlayer avatar) => avatar.GuildId;
}
