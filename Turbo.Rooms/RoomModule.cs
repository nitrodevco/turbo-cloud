using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Games;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Texts;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Games;
using Turbo.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Plugins;
using Turbo.Rooms.Providers;
using Turbo.Rooms.Wired.Variables;
using Turbo.Runtime.AssemblyProcessing;

namespace Turbo.Rooms;

public sealed class RoomModule : IHostPluginModule
{
    public string Key => "turbo-rooms";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<RoomConfig>(builder.Configuration.GetSection(RoomConfig.SECTION_NAME));
        services.Configure<PetConfig>(builder.Configuration.GetSection(PetConfig.SECTION_NAME));
        services.Configure<BotConfig>(builder.Configuration.GetSection(BotConfig.SECTION_NAME));
        services.Configure<WiredConfig>(builder.Configuration.GetSection(WiredConfig.SECTION_NAME));
        services.Configure<WiredChestConfig>(
            builder.Configuration.GetSection(WiredChestConfig.SECTION_NAME)
        );
        // The room directory times its hot-room window by it, and each room grain runs on it.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IRoomAvatarProvider, RoomAvatarProvider>();
        services.AddSingleton<IRoomItemsProvider, RoomItemsProvider>();
        services.AddSingleton<IRoomNpcProvider, RoomNpcProvider>();
        services.AddSingleton<IRoomModelProvider, RoomModelProvider>();
        services.AddSingleton<IRoomObjectLogicProvider, RoomObjectLogicProvider>();
        services.AddSingleton<IRoomEventListenerRegistry, RoomEventListenerRegistry>();
        services.AddSingleton<IRoomWiredVariablesProvider, RoomWiredVariablesProvider>();
        services.AddSingleton<IPetBreedProvider, PetBreedProvider>();
        services.AddSingleton<IPetSpeechProvider, PetSpeechProvider>();
        services.AddSingleton<IRecolorablePalettes, RecolorablePalettes>();

        services.AddSingleton<IAssemblyFeatureProcessor, RoomObjectLogicFeatureProcessor>();
        services.AddSingleton<IAssemblyFeatureProcessor, PluginLogicFeatureProcessor>();
        services.AddSingleton<IAssemblyFeatureProcessor, RoomEventListenerFeatureProcessor>();
        services.AddSingleton<IAssemblyFeatureProcessor, WiredVariableFeatureProcessor>();

        services.Configure<GameCenterConfig>(
            builder.Configuration.GetSection(GameCenterConfig.SECTION_NAME)
        );
        services.AddSingleton<GameRegistry>();
        services.AddSingleton<IGameRegistry>(sp => sp.GetRequiredService<GameRegistry>());
        services.AddSingleton<GameScoreStore>();
        services.AddSingleton<IGameResultService>(sp => sp.GetRequiredService<GameScoreStore>());
        services.AddSingleton<IGameLeaderboardService>(sp =>
            sp.GetRequiredService<GameScoreStore>()
        );
        services.AddSingleton<DefaultGameAccessPolicy>();
        services.AddSingleton<IAssemblyFeatureProcessor, GameFeatureProcessor>();

        services.AddSingleton<IRoomService, RoomService>();
        services.AddSingleton<IRoomOwnershipService, RoomOwnershipService>();
    }
}
