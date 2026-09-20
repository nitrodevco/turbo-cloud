using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// The sign the avatar is holding up, by the client's own numbering: the digits are zero to
/// ten and the rest are pictures. Holding no sign is <see cref="NO_SIGN"/>, because zero is
/// itself a sign.
/// </summary>
public sealed class UserSignVariable(RoomGrain roomGrain) : UserVariable<IRoomAvatar>(roomGrain)
{
    /// <summary>The highest sign the client's editor offers; the list is the signs it can draw.</summary>
    private const int MAX_SIGN = 17;

    private const int NO_SIGN = -1;

    protected override string VariableName => "@sign";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 50;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        WiredTextConnectors.ForIds(
            _roomGrain._hotelTextProvider,
            WiredTextConnectors.SIGN_KEY,
            Enumerable.Range(0, MAX_SIGN + 1)
        );

    protected override WiredVariableValue GetValueForAvatar(IRoomAvatar avatar) =>
        WiredVariableValue.Parse(
            avatar.Statuses.TryGetValue(AvatarStatusType.Sign, out var sign)
            && int.TryParse(sign, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                ? id
                : NO_SIGN
        );
}
