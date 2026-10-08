using System;
using System.Globalization;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Room;

/// <summary>
/// <c>@current_time.&lt;field&gt;</c>: one calendar field of the time now, in the room's wired
/// time zone. The Wired Faculty tutorial "Real-Time Clock" (11/03/2025, still the base of
/// "Multi Clock Timezones", 24/07/2026) reads <c>@current_time.hour_of_day</c> from the internal
/// variables after setting the room's time zone. The fields are the ones the time utilities
/// add-on names (its editor's <c>SubVariableParam</c>s), counted the same way.
/// </summary>
public abstract class RoomCurrentTimeFieldVariable(RoomGrain roomGrain) : RoomVariable(roomGrain)
{
    protected abstract string Field { get; }

    protected override string VariableName => "@current_time." + Field;

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Base;

    // Right after @current_time (4) and before @wired_timer (6).
    protected override ushort Order => 5;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.AlwaysAvailable;

    protected override WiredVariableValue GetValueForRoom(RoomGrain roomGrain) =>
        Read(roomGrain.WiredSystem.GetRoomLocalTime());

    protected abstract int Read(DateTimeOffset local);

    /// <summary>Monday is 1 and Sunday 7, as the time utilities add-on counts.</summary>
    protected static int DayOfWeek(DateTimeOffset local) => ((int)local.DayOfWeek + 6) % 7 + 1;

    protected static int WeekOfYear(DateTimeOffset local) => ISOWeek.GetWeekOfYear(local.DateTime);
}
