using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>The avatar's gender: male is zero and female is one, as the client's own figure data numbers them. Anything that is not a player has none.</summary>
public sealed class UserGenderVariable(RoomGrain roomGrain)
    : UserValueVariable<IRoomAvatar>(roomGrain)
{
    protected override string VariableName => "@gender";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    protected override ushort Order => 70;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.AlwaysAvailable
        | WiredVariableFlags.HasTextConnector;

    /// <summary>What an avatar that has no gender of its own reads as.</summary>
    private const int UNKNOWN_GENDER = -1;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        new()
        {
            [WiredVariableValue.Parse(UNKNOWN_GENDER)] = "Unknown",
            [WiredVariableValue.Parse((int)AvatarGenderType.Male)] = "Male",
            [WiredVariableValue.Parse((int)AvatarGenderType.Female)] = "Female",
        };

    protected override WiredVariableValue GetValueForAvatar(IRoomAvatar avatar) =>
        WiredVariableValue.Parse(
            avatar is IRoomPlayer player ? (int)player.Gender : UNKNOWN_GENDER
        );
}
