using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Gamedata.Api;
using Turbo.Gamedata.Assets;
using Turbo.Gamedata.ChatStyles;
using Turbo.Gamedata.ClientModules;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Figures;
using Turbo.Gamedata.Files;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.Habbo.Client;
using Turbo.Gamedata.History;
using Turbo.Gamedata.HotelView;
using Turbo.Gamedata.Plugins;
using Turbo.Gamedata.Products;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Texts;

namespace Turbo.Gamedata;

/// <summary>
/// The hotel's gamedata, kept in the database and built from it: Habbo's releases checked for
/// updates, its furniture taken in without losing the hotel's own changes, every change recorded
/// and rolled back as a set, and the files the client loads (FurnitureData, its offers stamped
/// from the catalog; the external variables, its configuration) served by hash from a small web
/// host of its own. Staff work with it in the
/// admin panel; the host is off unless <c>Turbo:Gamedata:Enabled</c>.
/// </summary>
public sealed class GamedataModule : IHostPluginModule
{
    public string Key => "turbo-gamedata";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<GamedataConfig>(
            builder.Configuration.GetSection(GamedataConfig.SECTION_NAME)
        );
        services.AddAssetBundleConfiguration(builder);

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(
            HabboGamedataClient.HTTP_CLIENT,
            (sp, http) =>
            {
                http.Timeout = TimeSpan.FromSeconds(
                    sp.GetRequiredService<IOptions<GamedataConfig>>().Value.HabboTimeoutSeconds
                );
                http.DefaultRequestHeaders.UserAgent.ParseAdd(HabboGamedataClient.USER_AGENT);
            }
        );
        services.AddHttpClient(
            HabboClientService.HTTP_CLIENT,
            (sp, http) =>
            {
                http.Timeout = TimeSpan.FromMinutes(
                    Math.Max(
                        1,
                        sp.GetRequiredService<
                            IOptions<GamedataConfig>
                        >().Value.ClientDownloadMinutes
                    )
                );
                http.DefaultRequestHeaders.UserAgent.ParseAdd(HabboGamedataClient.USER_AGENT);
            }
        );
        services.AddCloudflarePurgeClient();
        services.AddSingleton<HabboGamedataClient>();
        services.AddSingleton<HabboClientService>();
        services.AddSingleton<IHabboClientService>(sp =>
            sp.GetRequiredService<HabboClientService>()
        );
        services.AddSingleton<ClientBundleBuilder>();
        services.AddSingleton<AssetDiscoveryService>();
        services.AddSingleton<IHabboClientBuilds>(sp =>
            sp.GetRequiredService<ClientBundleBuilder>()
        );
        services.AddSingleton<GamedataWriteLock>();
        services.AddSingleton<FurnitureOfferCatalog>();
        services.AddSingleton<HabboReleaseItems>();
        services.AddSingleton<FurnitureStatusIndex>();
        services.AddSingleton<HabboFurnitureFiles>();
        services.AddSingleton<IHabboReleaseService, HabboReleaseService>();
        services.AddSingleton<IThemeFileService, GamedataFileService>();
        services.AddSingleton<IGamedataFileService>(sp =>
            sp.GetRequiredService<IThemeFileService>()
        );
        services.AddSingleton<IGamedataFurnitureService, GamedataFurnitureService>();
        services.AddSingleton<IGamedataHistoryService, GamedataHistoryService>();
        services.AddSingleton<IGamedataTextService, GamedataTextService>();
        services.AddSingleton<IGamedataProductService, GamedataProductService>();
        services.AddSingleton<IGamedataVariableService, GamedataVariableService>();
        services.AddSingleton<IHabboVariableService, HabboVariableService>();
        services.AddSingleton<IHotelViewService, HotelViewService>();
        services.AddSingleton<IHotelTextProvider, HotelTextProvider>();
        services.AddSingleton<IGamedataFigureService, GamedataFigureService>();
        services.AddSingleton<IFigureDataProvider, FigureDataProvider>();
        services.AddSingleton<IGamedataImportJobs, FurnitureImportJobs>();
        services.AddAssetBundleServices();
        services.AddSingleton<IChatStyleService, ChatStyleService>();
        services.AddAssetPublishingServices();
        services.AddHostedService<HabboReleaseWatcher>();
        services.AddHostedService<ClientBuildAfterDownload>();
        services.AddSingleton<ClientModuleRegistry>();
        services.AddSingleton<IClientModuleDirectory>(sp =>
            sp.GetRequiredService<ClientModuleRegistry>()
        );
        services.AddSingleton<IClientModuleRegistry>(sp =>
            sp.GetRequiredService<ClientModuleRegistry>()
        );
        services.AddHostedService<ClientModuleBroadcast>();
        services.AddHostedService<ThemeAdoption>();
        services.AddSingleton<LoadedPluginGamedata>();
        services.AddSingleton<PluginGamedataSeeds>();
        services.AddSingleton<IPluginGamedataSeeds>(sp =>
            sp.GetRequiredService<PluginGamedataSeeds>()
        );
        services.AddSingleton<IPluginDataOwner>(sp => sp.GetRequiredService<PluginGamedataSeeds>());
        services.AddSingleton<IPluginStyles, PluginStyleService>();
        services.AddSingleton<IThemeAudiences, ThemeAudienceService>();
        services.AddSingleton<ThemeLiveReload>();
        services.AddSingleton<IThemeLiveReload>(sp => sp.GetRequiredService<ThemeLiveReload>());
        services.AddHostedService(sp => sp.GetRequiredService<ThemeLiveReload>());
        services.AddHostedService<GamedataServer>();
    }
}
