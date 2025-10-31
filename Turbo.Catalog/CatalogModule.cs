using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Catalog.Abstractions;
using Turbo.Contracts.Enums.Catalog;
using Turbo.Contracts.Plugins;
using Turbo.Database.Context;

namespace Turbo.Catalog;

public sealed class CatalogModule : IHostPluginModule
{
    public string Key => "turbo-catalog";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ICatalogService, CatalogService>();
        services.AddSingleton<ICatalogProvider>(sp => new CatalogProvider(
            sp.GetRequiredService<IDbContextFactory<TurboDbContext>>(),
            CatalogTypeEnum.Normal
        ));
        services.AddHostedService<CatalogBootstrapper>();
    }
}
