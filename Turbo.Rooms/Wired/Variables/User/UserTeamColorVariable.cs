using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>The game team the player is on, by its colour; not held while they are on none.</summary>
public sealed class UserTeamColorVariable(RoomGrain roomGrain)
    : UserValueVariable<IRoomPlayer>(roomGrain)
{
    protected override string VariableName => "@team.color";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 140;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        Enum.GetValues<GameTeamType>()
            .ToDictionary(v => WiredVariableValue.Parse((int)v), v => v.ToString());

    protected override WiredVariableValue GetValueForAvatar(IRoomPlayer avatar) =>
        WiredVariableValue.Parse((int)GameSystem.GetTeam(avatar.PlayerId));

    // Held only while the player is on a team: the official client lists no team variables for
    // a player who is on none.
    protected override bool TryGetAvatarForKey(
        in WiredVariableKey key,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IRoomPlayer? avatar
    ) =>
        base.TryGetAvatarForKey(key, out avatar)
        && GameSystem.GetTeam(avatar.PlayerId) != GameTeamType.None;
}
