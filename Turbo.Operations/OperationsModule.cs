using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Turbo.Contracts.Plugins;
using Turbo.Operations.Configuration;
using Turbo.Operations.Movements;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Movements;

namespace Turbo.Operations;

/// <summary>
/// Running the hotel: sanctions, the word filter, the maintenance and shutdown countdown, the operator
/// commands (<c>docs/commands.md</c> section 4.1) that put them in staff's hands, and handing the
/// asset movement outbox to plugins.
/// </summary>
public sealed class OperationsModule : IHostPluginModule
{
    public string Key => "turbo-operations";

    public void ConfigureServices(IServiceCollection services, HostApplicationBuilder builder)
    {
        services.Configure<OperationsConfig>(
            builder.Configuration.GetSection(OperationsConfig.SECTION_NAME)
        );

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISanctionService, SanctionService>();
        services.AddSingleton<ICallForHelpService, CallForHelpService>();
        services.AddSingleton<IModerationToolService, ModerationToolService>();
        services.AddSingleton<IModerationIssueService, ModerationIssueService>();
        services.AddSingleton<IUserClassificationService, UserClassificationService>();
        services.AddSingleton<IWordFilter, WordFilter>();

        // One instance answers as the availability and runs as the countdown's background loop.
        services.AddSingleton<HotelAvailabilityService>();
        services.AddSingleton<IHotelAvailability>(sp =>
            sp.GetRequiredService<HotelAvailabilityService>()
        );
        services.AddHostedService(sp => sp.GetRequiredService<HotelAvailabilityService>());

        // Plugins register asset movement consumers; each dispatcher activation has its own
        // cursors (docs/movements.md).
        services.AddSingleton<AssetMovementConsumerRegistry>();
        services.AddSingleton<IAssetMovementConsumerRegistry>(sp =>
            sp.GetRequiredService<AssetMovementConsumerRegistry>()
        );
        services.AddTransient<AssetMovementDispatcher>();

        // A plugin that undoes movements moves assets back through this (docs/movements.md).
        services.AddSingleton<IAssetReversalService, AssetReversalService>();
    }
}
