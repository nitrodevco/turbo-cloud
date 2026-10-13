using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Room;

public sealed class RoomCurrentTimeDayOfWeekVariable(RoomGrain roomGrain)
    : RoomCurrentTimeFieldVariable(roomGrain)
{
    protected override string Field => "day_of_week";

    protected override ushort Order => 96;

    // Habbo's labels, from 1 (hhus, WIN63-202610121709).
    private static readonly string[] Labels =
    [
        "Monday",
        "Tuesday",
        "Wednesday",
        "Thursday",
        "Friday",
        "Saturday",
        "Sunday",
    ];

    protected override WiredVariableFlags Flags => base.Flags | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        Enumerable
            .Range(1, Labels.Length)
            .ToDictionary(x => WiredVariableValue.Parse(x), x => Labels[x - 1]);

    protected override int Read(DateTimeOffset local) => DayOfWeek(local);
}
