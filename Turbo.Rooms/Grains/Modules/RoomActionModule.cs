namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomActionModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;
}
