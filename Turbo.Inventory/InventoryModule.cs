using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Inventory.Configuration;
using Turbo.Inventory.Furniture;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Furniture;

namespace Turbo.Inventory;

public sealed class InventoryModule : IHostPluginModule
{
    public string Key => "turbo-inventory";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<InventoryConfig>(
            builder.Configuration.GetSection(InventoryConfig.SECTION_NAME)
        );

        services.AddSingleton<IFurnitureItemsLoader, FurnitureItemsLoader>();
        services.AddSingleton<IInventoryService, InventoryService>();
    }
}
