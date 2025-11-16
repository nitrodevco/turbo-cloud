using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Pipeline;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Furniture;
using Turbo.Rooms.Furniture.Logic;
using Turbo.Rooms.Mapping;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Rooms;

public sealed class RoomModule : IHostPluginModule
{
    public string Key => "turbo-rooms";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<RoomConfig>(builder.Configuration.GetSection(RoomConfig.SECTION_NAME));

        services.AddSingleton<IRoomItemsLoader, RoomItemsLoader>();
        services.AddSingleton<IRoomService, RoomService>();
        services.AddSingleton<IRoomModelProvider, RoomModelProvider>();

        services.AddSingleton<IFurnitureLogicFactory, FurnitureLogicFactory>();
        services.AddSingleton<IAssemblyFeatureProcessor, FurnitureLogicFeatureProcessor>();
    }
}
