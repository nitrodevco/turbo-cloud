using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Rooms.Abstractions;

namespace Turbo.Rooms;

public sealed class RoomModule : IHostPluginModule
{
    public string Key => "turbo-rooms";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IRoomService, RoomService>();
        services.AddSingleton<IRoomModelProvider, RoomModelProvider>();
    }
}
