using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Inventory.Abstractions;

namespace Turbo.Inventory;

public sealed class InventoryModule : IHostPluginModule
{
    public string Key => "turbo-inventory";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IInventoryService, InventoryService>();
    }
}
