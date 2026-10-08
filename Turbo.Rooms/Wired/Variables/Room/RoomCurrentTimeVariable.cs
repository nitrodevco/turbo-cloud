using System;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Room;

/// <summary>
/// <c>@current_time</c>: the time now in unix milliseconds. Builders time a run by keeping it
/// when it starts and subtracting it from itself at the end (Wired Faculty, #help 07/02/2026);
/// it takes the 64 bits a value has.
/// </summary>
public sealed class RoomCurrentTimeVariable(RoomGrain roomGrain) : RoomVariable(roomGrain)
{
    protected override string VariableName => "@current_time";
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;
    protected override ushort Order => 20;
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForRoom(RoomGrain roomGrain) =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
