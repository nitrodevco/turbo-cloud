using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Furniture.Abstractions;

namespace Turbo.Furniture;

public sealed class FurnitureModule : IHostPluginModule
{
    public string Key => "turbo-furniture";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IFurnitureService, FurnitureService>();
        services.AddSingleton<IFurnitureProvider, FurnitureProvider>();
    }
}
