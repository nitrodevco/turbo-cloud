using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Inventory.Configuration;
using Turbo.Inventory.Factories;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Factories;

namespace Turbo.Inventory;

public sealed class InventoryModule : IHostPluginModule
{
    public string Key => "turbo-inventory";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<InventoryConfig>(
            builder.Configuration.GetSection(InventoryConfig.SECTION_NAME)
        );

        services.AddSingleton<IInventoryFurnitureLoader, InventoryFurnitureLoader>();
        services.AddSingleton<IInventoryService, InventoryService>();
    }
}
