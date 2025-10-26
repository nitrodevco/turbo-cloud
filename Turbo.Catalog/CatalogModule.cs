using Microsoft.Extensions.DependencyInjection;
using Turbo.Catalog.Abstractions;
using Turbo.Contracts.Plugins;

namespace Turbo.Catalog;

public sealed class CatalogModule : IHostPluginModule
{
    public string Key => "turbo-catalog";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ICatalogFactory, CatalogFactory>();
        services.AddSingleton<ICatalogService, CatalogService>();
        services.AddHostedService<CatalogBootstrapper>();
    }
}
