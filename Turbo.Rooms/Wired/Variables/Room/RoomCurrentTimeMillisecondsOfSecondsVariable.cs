using System;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Room;

public sealed class RoomCurrentTimeMillisecondsOfSecondsVariable(RoomGrain roomGrain)
    : RoomCurrentTimeFieldVariable(roomGrain)
{
    protected override string Field => "milliseconds_of_seconds";

    protected override ushort Order => 100;

    protected override int Read(DateTimeOffset local) => local.Millisecond;
}
