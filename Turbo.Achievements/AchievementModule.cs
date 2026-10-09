using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
                    && x.MaxMatchValues > 0
                    && x.FactRetentionDays >= 0,
                "Achievement limits must be positive."
            )
            .Validate(
                x =>
                    string.IsNullOrWhiteSpace(x.BadgeAssetUrl)
                    || AchievementBadgeAssets.IsValidUrlTemplate(x.BadgeAssetUrl),
                $"Turbo:Achievements:BadgeAssetUrl must be an http(s) URL containing {AchievementBadgeAssets.BADGE_NAME_TOKEN}."
            )
            .ValidateOnStart();
        services
            .AddOptions<DailyTaskConfig>()
            .Bind(builder.Configuration.GetSection(DailyTaskConfig.SECTION_NAME))
            .Validate(
                x =>
                    x.TasksPerDay >= 0
                    && x.HcDucketMultiplier >= 1
                    && x.UnclaimedKeepDays >= 0
                    && x.ResetTimeUtc >= TimeSpan.Zero
                    && x.ResetTimeUtc < TimeSpan.FromDays(1),
                "Turbo:DailyTasks: counts must not be negative and the reset must be a time of day."
            )
            .ValidateOnStart();
        services
            .AddOptions<RewardTrackConfig>()
            .Bind(builder.Configuration.GetSection(RewardTrackConfig.SECTION_NAME))
            .Validate(
                x => x.Problem() is null,
                "Turbo:RewardTracks is invalid: unique ids, levels counting up from 1, premium boost of at least 1, no negative points, costs or amounts."
            )
            .ValidateOnStart();
        services.AddSingleton<IAchievementFactListener, RewardTrackFactListener>();
        services.TryAddSingleton(TimeProvider.System);
        // Installing the Habbo pack is on unless the hotel turns it off.
        if (
            !bool.TryParse(
                builder.Configuration.GetSection(AchievementConfig.SECTION_NAME)[
                    nameof(AchievementConfig.InstallDefaults)
                ],
                out var installDefaults
            ) || installDefaults
        )
            services.AddSingleton<IAchievementPack, HabboAchievementPack>();
        services.AddSingleton<IAchievementPackRegistry, AchievementPackRegistry>();
        services.AddSingleton<AchievementBadgeAssets>();
        services.AddSingleton<IAchievementCatalog, AchievementCatalog>();
        services.AddSingleton<IAchievementFactRecorder, AchievementFactRecorder>();
        services.AddSingleton<IAchievementFacts, AchievementFacts>();
        services.AddSingleton<AchievementSync>();
        services.AddSingleton<IAchievementRewardRegistry, AchievementRewardRegistry>();
        services.AddSingleton<IAchievementObserverRegistry, AchievementObserverRegistry>();
        services.AddSingleton<AchievementStateEvaluator>();
        services.AddHostedService<AchievementRecoveryService>();
    }
}
