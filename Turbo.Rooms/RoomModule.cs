using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Logic;
using Turbo.Rooms.Providers;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Providers;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Rooms;

public sealed class RoomModule : IHostPluginModule
{
    public string Key => "turbo-rooms";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<RoomConfig>(builder.Configuration.GetSection(RoomConfig.SECTION_NAME));

        services.AddSingleton<IRoomAvatarProvider, RoomAvatarProvider>();
        services.AddSingleton<IRoomItemsProvider, RoomItemsProvider>();
        services.AddSingleton<IRoomModelProvider, RoomModelProvider>();
        services.AddSingleton<IRoomObjectLogicProvider, RoomObjectLogicProvider>();
        services.AddSingleton<IWiredDefinitionProvider, WiredDefinitionProvider>();

        services.AddSingleton<IAssemblyFeatureProcessor, RoomObjectLogicFeatureProcessor>();
        services.AddSingleton<IAssemblyFeatureProcessor, WiredFeatureProcessor>();

        services.AddSingleton<IRoomService, RoomService>();
    }
}
