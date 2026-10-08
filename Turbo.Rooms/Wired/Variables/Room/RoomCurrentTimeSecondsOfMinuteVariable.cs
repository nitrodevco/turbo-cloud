using System;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Room;

public sealed class RoomCurrentTimeSecondsOfMinuteVariable(RoomGrain roomGrain)
    : RoomCurrentTimeFieldVariable(roomGrain)
{
    protected override string Field => "seconds_of_minute";

    protected override ushort Order => 99;

    protected override int Read(DateTimeOffset local) => local.Second;
}
