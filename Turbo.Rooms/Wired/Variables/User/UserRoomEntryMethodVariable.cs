using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// How the player came to be in this room, which does not change while they are here. Only a
/// furni that sends someone somewhere makes this anything but the default, so a player who
/// walked in through the navigator reads as zero.
/// </summary>
public sealed class UserRoomEntryMethodVariable(RoomGrain roomGrain)
    : UserValueVariable<IRoomPlayer>(roomGrain)
{
    protected override string VariableName => "@room_entry.method";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 30;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.AlwaysAvailable
        | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        // Habbo's labels (hhus, WIN63-202610121709).
        new()
        {
            [WiredVariableValue.Parse((int)RoomEntryMethodType.Default)] = "Default",
            [WiredVariableValue.Parse((int)RoomEntryMethodType.Teleport)] = "Teleport",
            [WiredVariableValue.Parse((int)RoomEntryMethodType.RoomNetwork)] = "Room Network",
        };

    protected override WiredVariableValue GetValueForAvatar(IRoomPlayer avatar) =>
        WiredVariableValue.Parse((int)avatar.RoomEntry.Method);
}
