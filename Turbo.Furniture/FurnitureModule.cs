using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Furniture.Abstractions;
using Turbo.Furniture.Configuration;

namespace Turbo.Furniture;

public sealed class FurnitureModule : IHostPluginModule
{
    public string Key => "turbo-furniture";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<FurnitureConfig>(
            builder.Configuration.GetSection(FurnitureConfig.SECTION_NAME)
        );

        services.AddSingleton<IFurnitureService, FurnitureService>();
        services.AddSingleton<IFurnitureProvider, FurnitureProvider>();
    }
}
