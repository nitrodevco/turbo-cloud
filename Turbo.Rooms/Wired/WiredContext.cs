using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired;

internal abstract class WiredContext
{
    public required RoomGrain Room { get; init; }
}
