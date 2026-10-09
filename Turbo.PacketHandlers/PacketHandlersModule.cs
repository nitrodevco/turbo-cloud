using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.PacketHandlers.Camera;

namespace Turbo.PacketHandlers;

public sealed class PacketHandlersModule : IHostPluginModule
{
    public string Key => "turbo-packet-handlers";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<CameraConfig>(
            builder.Configuration.GetSection(CameraConfig.SECTION_NAME)
        );
        services.AddSingleton<CameraPhotoStore>();
        services.AddSingleton<CameraRenderer>();
    }
}
