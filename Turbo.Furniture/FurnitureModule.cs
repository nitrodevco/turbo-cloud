using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Furniture.Configuration;
using Turbo.Furniture.StuffData;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.StuffData;

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
        services.AddSingleton<IFurnitureDefinitionProvider, FurnitureDefinitionProvider>();
        services.AddSingleton<IStuffDataFactory, StuffDataFactory>();
    }
}
