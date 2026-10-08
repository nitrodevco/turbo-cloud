using System;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Room;

public sealed class RoomCurrentTimeDayOfMonthVariable(RoomGrain roomGrain)
    : RoomCurrentTimeFieldVariable(roomGrain)
{
    protected override string Field => "day_of_month";

    protected override int Read(DateTimeOffset local) => local.Day;
}
