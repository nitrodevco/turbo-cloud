using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Rooms.Abstractions;
using Turbo.Rooms.Abstractions.Furniture;
using Turbo.Rooms.Furniture;
using Turbo.Rooms.Mapping;

namespace Turbo.Rooms;

public sealed class RoomModule : IHostPluginModule
{
    public string Key => "turbo-rooms";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.AddSingleton<IRoomFloorItemsLoader, RoomFloorItemsLoader>();

        services.AddSingleton<IRoomService, RoomService>();
        services.AddSingleton<IRoomModelProvider, RoomModelProvider>();
    }
}
