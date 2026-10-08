using System;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Room;

public sealed class RoomCurrentTimeMonthOfYearVariable(RoomGrain roomGrain)
    : RoomCurrentTimeFieldVariable(roomGrain)
{
    protected override string Field => "month_of_year";

    protected override ushort Order => 92;

    protected override int Read(DateTimeOffset local) => local.Month;
}
