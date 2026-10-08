using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// <c>@team.type</c>: the kind of team the player is on, as the official client's Creator Tools
/// number it: 0 Battle Banzai, 1 Freeze, 2 Tagging, 3 Swimming, 4 Wired. Read only; held only
/// while the player is on a team.
/// </summary>
public sealed class UserTeamTypeVariable(RoomGrain roomGrain)
    : UserValueVariable<IRoomPlayer>(roomGrain)
{
    private const int BATTLE_BANZAI = 0;
    private const int FREEZE = 1;
    private const int WIRED = 4;

    protected override string VariableName => "@team.type";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    // After @team.color (140), before @handitem (120), as the official list has it.
    protected override ushort Order => 130;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        new()
        {
            [0] = "Battle Banzai",
            [1] = "Freeze",
            [2] = "Tagging",
            [3] = "Swimming",
            [4] = "Wired",
        };

    protected override bool TryGetAvatarForKey(
        in WiredVariableKey key,
        [NotNullWhen(true)] out IRoomPlayer? avatar
    ) =>
        base.TryGetAvatarForKey(key, out avatar)
        && GameSystem.GetTeam(avatar.PlayerId) != GameTeamType.None;

    protected override WiredVariableValue GetValueForAvatar(IRoomPlayer avatar) =>
        GameSystem.GetTeamType(avatar.PlayerId) switch
        {
            WiredTeamType.BattleBanzai => BATTLE_BANZAI,
            WiredTeamType.Freeze => FREEZE,
            _ => WIRED,
        };
}
