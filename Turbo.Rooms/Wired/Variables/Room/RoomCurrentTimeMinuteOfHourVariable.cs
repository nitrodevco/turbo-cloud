using System;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Room;

public sealed class RoomCurrentTimeMinuteOfHourVariable(RoomGrain roomGrain)
    : RoomCurrentTimeFieldVariable(roomGrain)
{
    protected override string Field => "minute_of_hour";

    protected override ushort Order => 98;

    protected override int Read(DateTimeOffset local) => local.Minute;
}
