using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Enums.Catalog;
using Turbo.Contracts.Plugins;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture;

namespace Turbo.Catalog;

public sealed class CatalogModule : IHostPluginModule
{
    public string Key => "turbo-catalog";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.AddSingleton<ICatalogService, CatalogService>();
        services.AddSingleton<ICatalogProvider<NormalCatalog>>(
            sp => new CatalogProvider<NormalCatalog>(
                sp.GetRequiredService<IDbContextFactory<TurboDbContext>>(),
                sp.GetRequiredService<ILogger<ICatalogProvider<NormalCatalog>>>(),
                sp.GetRequiredService<IFurnitureDefinitionProvider>(),
                CatalogTypeEnum.Normal
            )
        );
    }
}
