using Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Factories;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomActionModule(
    RoomGrain roomGrain,
    RoomLiveState liveState,
    RoomSecurityModule securityModule,
    RoomFurniModule furniModule,
    IGrainFactory grainFactory,
    IRoomItemsLoader itemsLoader
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomLiveState _state = liveState;
    private readonly RoomSecurityModule _securityModule = securityModule;
    private readonly RoomFurniModule _furniModule = furniModule;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IRoomItemsLoader _itemsLoader = itemsLoader;
}
