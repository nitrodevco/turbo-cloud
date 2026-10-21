using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.PacketHandlers.Camera;
using Turbo.Primitives.Camera;
using Turbo.Primitives.Rooms;

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
        services.AddSingleton<ICameraRenderer, CameraRenderer>();
        services.AddSingleton<CameraCaptureJobs>();
        services.AddSingleton<RoomThumbnailStore>();
        services.AddSingleton<IRoomThumbnailFiles>(sp =>
            sp.GetRequiredService<RoomThumbnailStore>()
        );
        services.AddSingleton<ICameraPhotoFiles, CameraPhotoFiles>();
    }
}
