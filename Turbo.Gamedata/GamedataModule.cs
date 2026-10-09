using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Gamedata.Api;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Figures;
using Turbo.Gamedata.Files;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.History;
using Turbo.Gamedata.HotelView;
using Turbo.Gamedata.Products;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Gamedata;
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
        services.AddSingleton<HabboGamedataClient>();
        services.AddSingleton<GamedataWriteLock>();
        services.AddSingleton<FurnitureOfferCatalog>();
        services.AddSingleton<HabboReleaseItems>();
        services.AddSingleton<HabboFurnitureFiles>();
        services.AddSingleton<IHabboReleaseService, HabboReleaseService>();
        services.AddSingleton<IGamedataFileService, GamedataFileService>();
        services.AddSingleton<IGamedataFurnitureService, GamedataFurnitureService>();
        services.AddSingleton<IGamedataHistoryService, GamedataHistoryService>();
        services.AddSingleton<IGamedataTextService, GamedataTextService>();
        services.AddSingleton<IGamedataProductService, GamedataProductService>();
        services.AddSingleton<IGamedataVariableService, GamedataVariableService>();
        services.AddSingleton<IHotelViewService, HotelViewService>();
        services.AddSingleton<IHotelTextProvider, HotelTextProvider>();
        services.AddSingleton<IGamedataFigureService, GamedataFigureService>();
        services.AddSingleton<IFigureDataProvider, FigureDataProvider>();
        services.AddSingleton<IGamedataImportJobs, FurnitureImportJobs>();
        services.AddHostedService<HabboReleaseWatcher>();
        services.AddHostedService<GamedataServer>();
    }
}
