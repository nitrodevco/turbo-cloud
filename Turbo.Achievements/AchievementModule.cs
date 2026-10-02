using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Turbo.Achievements.Configuration;
using Turbo.Contracts.Plugins;
using Turbo.Database.Achievements;
using Turbo.Primitives.Achievements;

namespace Turbo.Achievements;

public sealed class AchievementModule : IHostPluginModule
{
    public string Key => "turbo-achievements";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services
            .AddOptions<AchievementConfig>()
            .Bind(builder.Configuration.GetSection(AchievementConfig.SECTION_NAME))
            .Validate(
                x =>
                    x.RecoverySeconds > 0
                    && x.RecoveryBatchSize > 0
                    && x.FactBatchSize > 0
                    && x.MaxDefinitions > 0
                    && x.MaxDistinctValues > 0
                    && x.FactRetentionDays >= 0,
                "Achievement limits must be positive."
            )
            .ValidateOnStart();
        services.AddSingleton<IAchievementCatalog, AchievementCatalog>();
        services.AddSingleton<IAchievementFactRecorder, AchievementFactRecorder>();
        services.AddSingleton<IAchievementRewardRegistry, AchievementRewardRegistry>();
        services.AddSingleton<IAchievementObserverRegistry, AchievementObserverRegistry>();
        services.AddSingleton<AchievementStateEvaluator>();
        services.AddHostedService<AchievementRecoveryService>();
    }
}
