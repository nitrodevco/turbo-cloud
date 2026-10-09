using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Editing;
using Turbo.Catalog.Providers;
using Turbo.Catalog.Reception;
using Turbo.Contracts.Plugins;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Hotel;

namespace Turbo.Catalog;

public sealed class CatalogModule : IHostPluginModule
{
    public string Key => "turbo-catalog";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<CatalogConfig>(
            builder.Configuration.GetSection(CatalogConfig.SECTION_NAME)
        );

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ICatalogService, CatalogService>();
        services.AddSingleton<ICatalogEditService, CatalogEditService>();
        services.AddSingleton<IBonusRareService, BonusRareService>();
        services.AddSingleton<IExpiringPageService, ExpiringPageService>();
        services.AddSingleton<IPromoArticleService, PromoArticleService>();
        services.AddSingleton<ICommunityGoalService, CommunityGoalService>();
        services.AddSingleton<IGiftWrappingProvider, GiftWrappingProvider>();
        services.AddSingleton<ICatalogSnapshotProvider<NormalCatalog>>(
            sp => new CatalogSnapshotProvider<NormalCatalog>(
                sp.GetRequiredService<IDbContextFactory<TurboDbContext>>(),
                sp.GetRequiredService<ILogger<ICatalogSnapshotProvider<NormalCatalog>>>(),
                sp.GetRequiredService<IFurnitureDefinitionProvider>(),
                CatalogType.Normal
            )
        );
        services.AddSingleton<ICatalogSnapshotProvider<BuildersClubCatalog>>(
            sp => new CatalogSnapshotProvider<BuildersClubCatalog>(
                sp.GetRequiredService<IDbContextFactory<TurboDbContext>>(),
                sp.GetRequiredService<ILogger<ICatalogSnapshotProvider<BuildersClubCatalog>>>(),
                sp.GetRequiredService<IFurnitureDefinitionProvider>(),
                CatalogType.BuildersClub
            )
        );
    }
}
