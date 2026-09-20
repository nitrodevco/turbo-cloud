using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>The dance the avatar is doing, by the client's own numbering. Zero is standing still.</summary>
public sealed class UserDanceVariable(RoomGrain roomGrain) : UserVariable<IRoomAvatar>(roomGrain)
{
    protected override string VariableName => "@dance";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 60;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        WiredTextConnectors.ForIds(
            _roomGrain._hotelTextProvider,
            WiredTextConnectors.DANCE_KEY,
            Enum.GetValues<AvatarDanceType>().Select(x => (int)x)
        );

    protected override WiredVariableValue GetValueForAvatar(IRoomAvatar avatar) =>
        WiredVariableValue.Parse((int)avatar.DanceType);
}
