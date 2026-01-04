using Turbo.Primitives.Rooms;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomActionModule(
    RoomGrain roomGrain,
    RoomLiveState liveState,
    RoomSecurityModule securityModule,
    RoomFurniModule furniModule
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomLiveState _state = liveState;
    private readonly RoomSecurityModule _securityModule = securityModule;
    private readonly RoomFurniModule _furniModule = furniModule;
}
